using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
	public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		private readonly IDataContext dataContext;
		private readonly IPathProvider templatePathProvider;
		private readonly IViewGenerator viewGenerator;
		private readonly IConfiguration configuration;
		private readonly IPdfGenerator pdfGenerator;
		private readonly ILogger<PdfApplicationDocumentGenerator> logger;

		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			this.dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext)); ;
			this.templatePathProvider = templatePathProvider ?? throw new ArgumentNullException(templatePathProvider);
			this.viewGenerator = viewGenerator ?? throw new ArgumentNullException(nameof(viewGenerator)); ;
			this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); ;
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this.pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator)); ;
		}

		public byte[] Generate(Guid applicationId, string baseUri)
		{
			if (applicationId == Guid.Empty || String.IsNullOrEmpty(baseUri))
				return null;

			Application application = dataContext.Applications.Single(app => app.Id == applicationId);

			if (application != null)
			{

				if (baseUri.EndsWith("/"))
					baseUri = baseUri.Substring(baseUri.Length - 1);

				string view;

				if (application.State == ApplicationState.Pending)
				{
					string path = templatePathProvider.Get("PendingApplication");
					PendingApplicationViewModel vm = new PendingApplicationViewModel
					{
						ReferenceNumber = application.ReferenceNumber,
						State = application.State.ToDescription(),
						FullName = $"{application.Person.FirstName} {application.Person.Surname}"
						AppliedOn = application.Date,
						SupportEmail = configuration.SupportEmail,
						Signature = configuration.Signature
					};
					view = viewGenerator.GenerateFromPath($"{baseUri}{path}", vm);
				}
				else if (application.State == ApplicationState.Activated)
				{
					string path = templatePathProvider.Get("ActivatedApplication");
					ActivatedApplicationViewModel vm = new ActivatedApplicationViewModel
					{
						ReferenceNumber = application.ReferenceNumber,
						State = application.State.ToDescription(),
						FullName = $"{application.Person.FirstName} {application.Person.Surname}",
						LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
						PortfolioFunds = application.Products.SelectMany(p => p.Funds),
						PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
														.Select(f => (f.Amount - f.Fees) * configuration.TaxRate)
														.Sum(),
						AppliedOn = application.Date,
						SupportEmail = configuration.SupportEmail,
						Signature = configuration.Signature
					};
					view = viewGenerator.GenerateFromPath($"{baseUri}{path}", vm);
				}
				else if (application.State == ApplicationState.InReview)
				{
					var templatePath = templatePathProvider.Get("InReviewApplication");
					var inReviewMessage = "Your application has been placed in review" +
										application.CurrentReview.Reason switch
										{
											{ } reason when reason.Contains("address") =>
												" pending outstanding address verification for FICA purposes.",
											{ } reason when reason.Contains("bank") =>
												" pending outstanding bank account verification.",
											_ =>
												" because of suspicious account behaviour. Please contact support ASAP."
										};
					var inReviewApplicationViewModel = new InReviewApplicationViewModel();
					inReviewApplicationViewModel.ReferenceNumber = application.ReferenceNumber;
					inReviewApplicationViewModel.State = application.State.ToDescription();
					inReviewApplicationViewModel.FullName = $"{application.Person.FirstName} { application.Person.Surname}";
					inReviewApplicationViewModel.LegalEntity = application.IsLegalEntity ? application.LegalEntity : null;
					inReviewApplicationViewModel.PortfolioFunds = application.Products.SelectMany(p => p.Funds);
					inReviewApplicationViewModel.PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
						.Select(f => (f.Amount - f.Fees) * configuration.TaxRate)
						.Sum();
					inReviewApplicationViewModel.InReviewMessage = inReviewMessage;
					inReviewApplicationViewModel.InReviewInformation = application.CurrentReview;
					inReviewApplicationViewModel.AppliedOn = application.Date;
					inReviewApplicationViewModel.SupportEmail = configuration.SupportEmail;
					inReviewApplicationViewModel.Signature = configuration.Signature;
					view = viewGenerator.GenerateFromPath($"{baseUri}{templatePath}", inReviewApplicationViewModel);
				}
				else
				{
					logger.LogWarning(
						$"The application is in state '{application.State}' and no valid document can be generated for it.");
					return null;
				}

				var pdfOptions = new PdfOptions
				{
					PageNumbers = PageNumbers.Numeric,
					HeaderOptions = new HeaderOptions
					{
						HeaderRepeat = HeaderRepeat.FirstPageOnly,
						HeaderHtml = PdfConstants.Header
					}
				};
				var pdf = pdfGenerator.GenerateFromHtml(view, pdfOptions);
				return pdf.ToBytes();
			}
			else
			{

				logger.LogWarning(
					$"No application found for id '{applicationId}'");
				return null;
			}
		}
	}
}

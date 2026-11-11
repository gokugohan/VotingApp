using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Localization;

namespace VotingApp.Web.Localization
{
    /// <summary>
    /// Serves form labels ([Display(Name)]) and validation messages from the same translation files as
    /// the views, in the language of the current request.
    /// </summary>
    public sealed class CatalogLocalizer : IStringLocalizer
    {
        private readonly TranslationCatalog catalog;
        private readonly IHttpContextAccessor accessor;

        public CatalogLocalizer(TranslationCatalog catalog, IHttpContextAccessor accessor)
        {
            this.catalog = catalog;
            this.accessor = accessor;
        }

        private string Lang => Texts.Resolve(accessor.HttpContext);

        public LocalizedString this[string name]
        {
            get
            {
                var text = catalog.Translate(Lang, name);
                return new LocalizedString(name, text, resourceNotFound: ReferenceEquals(text, name));
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var s = this[name];
                return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, s.Value, arguments), s.ResourceNotFound);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public sealed class CatalogLocalizerFactory : IStringLocalizerFactory
    {
        private readonly CatalogLocalizer localizer;

        public CatalogLocalizerFactory(TranslationCatalog catalog, IHttpContextAccessor accessor) =>
            localizer = new CatalogLocalizer(catalog, accessor);

        public IStringLocalizer Create(Type resourceSource) => localizer;
        public IStringLocalizer Create(string baseName, string location) => localizer;
    }

    /// <summary>
    /// MVC only localizes validation messages that are set explicitly, so the common attributes get
    /// their usual English message as an explicit one (which the localizer then translates).
    /// </summary>
    public sealed class DefaultValidationMessages : IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (!string.IsNullOrEmpty(attribute.ErrorMessage) || attribute.ErrorMessageResourceType != null) continue;
                attribute.ErrorMessage = attribute switch
                {
                    RequiredAttribute => "The {0} field is required.",
                    RangeAttribute => "The field {0} must be between {1} and {2}.",
                    StringLengthAttribute s when s.MinimumLength > 0 => "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}.",
                    StringLengthAttribute => "The field {0} must be a string with a maximum length of {1}.",
                    EmailAddressAttribute => "The {0} field is not a valid e-mail address.",
                    CompareAttribute => "'{0}' and '{1}' do not match.",
                    _ => null,
                };
            }
        }
    }
}

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace VotingApp.Web.Localization
{
    /// <summary>
    /// How dates and numbers are written in each language. English follows British conventions
    /// (1,234.5; 1 May 2027); Portuguese follows Portugal (1 234,5; 1 de maio de 2027). .NET has no
    /// Tetun culture, so Tetun uses the Portuguese conventions with Tetun month and day names.
    /// </summary>
    public static class AppCultures
    {
        private static readonly CultureInfo English = CultureInfo.ReadOnly(new CultureInfo("en-GB"));
        private static readonly CultureInfo Portuguese = CultureInfo.ReadOnly(new CultureInfo("pt-PT"));
        private static readonly CultureInfo Tetun = BuildTetun();

        public static CultureInfo For(string language) => language switch
        {
            "pt" => Portuguese,
            "tet" => Tetun,
            _ => English,
        };

        /// <summary>Format of a full date ("1 May 2027", "1 de maio de 2027", "1 Maiu 2027").</summary>
        public static string LongDatePattern(string language) => language == "pt" ? "d 'de' MMMM 'de' yyyy" : "d MMMM yyyy";

        private static CultureInfo BuildTetun()
        {
            var culture = (CultureInfo)new CultureInfo("pt-PT").Clone();
            var months = new[] { "Janeiru", "Fevereiru", "Marsu", "Abríl", "Maiu", "Juñu", "Jullu", "Agostu", "Setembru", "Outubru", "Novembru", "Dezembru", "" };
            var shortMonths = new[] { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez", "" };
            var f = culture.DateTimeFormat;
            f.MonthNames = months;
            f.MonthGenitiveNames = months;
            f.AbbreviatedMonthNames = shortMonths;
            f.AbbreviatedMonthGenitiveNames = shortMonths;
            f.DayNames = new[] { "Domingu", "Segunda", "Tersa", "Kuarta", "Kinta", "Sesta", "Sábadu" };
            f.AbbreviatedDayNames = new[] { "Dom", "Seg", "Ter", "Kua", "Kin", "Ses", "Sáb" };
            f.ShortestDayNames = f.AbbreviatedDayNames;
            return CultureInfo.ReadOnly(culture);
        }

        /// <summary>Sets the culture of each request from the chosen language, so dates, numbers and messages agree.</summary>
        public static Task Apply(HttpContext context, Func<Task> next)
        {
            var culture = For(Texts.Resolve(context));
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return next();
        }
    }

    /// <summary>
    /// Posted forms are read with the invariant culture whatever the page language: browsers send
    /// numbers from number and date inputs as "4.5" and "2027-05-01", which a Portuguese reading would
    /// turn into 45. Values typed by people into forms here are whole numbers, dates or text.
    /// </summary>
    public sealed class InvariantFormValueProviderFactory : IValueProviderFactory
    {
        public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
        {
            var request = context.ActionContext.HttpContext.Request;
            return request.HasFormContentType ? AddAsync(context, request) : Task.CompletedTask;
        }

        private static async Task AddAsync(ValueProviderFactoryContext context, HttpRequest request)
        {
            IFormCollection form;
            try
            {
                form = await request.ReadFormAsync(context.ActionContext.HttpContext.RequestAborted);
            }
            catch (System.IO.InvalidDataException ex)
            {
                throw new ValueProviderException("Failed to read the request form. " + ex.Message, ex);
            }
            catch (System.IO.IOException ex)
            {
                throw new ValueProviderException("Failed to read the request form. " + ex.Message, ex);
            }
            context.ValueProviders.Add(new FormValueProvider(BindingSource.Form, form, CultureInfo.InvariantCulture));
        }
    }
}

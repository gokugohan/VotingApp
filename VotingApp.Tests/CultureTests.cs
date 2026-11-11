using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using VotingApp.Web.Localization;

namespace VotingApp.Tests
{
    public class CultureTests
    {
        private static readonly DateTime Day = new(2033, 5, 1);

        [Theory]
        [InlineData("en", "1 May 2033")]
        [InlineData("pt", "1 de maio de 2033")]
        [InlineData("tet", "1 Maiu 2033")]
        public void Full_dates_follow_the_language(string language, string expected) =>
            Assert.Equal(expected, Day.ToString(AppCultures.LongDatePattern(language), AppCultures.For(language)));

        [Fact]
        public void Portuguese_and_Tetun_write_numbers_the_Portuguese_way()
        {
            foreach (var language in new[] { "pt", "tet" })
            {
                var culture = AppCultures.For(language);
                Assert.Equal("4,5", 4.5m.ToString("0.0", culture));
                var thousands = 1234567L.ToString("N0", culture);
                Assert.DoesNotContain(",", thousands);
                Assert.Equal("1234567", new string(thousands.Where(char.IsDigit).ToArray()));
            }
            Assert.Equal("1,234,567", 1234567L.ToString("N0", AppCultures.For("en")));
            Assert.Equal("Segunda", Day.AddDays(1).ToString("dddd", AppCultures.For("tet")));
        }

        [Fact]
        public async Task Posted_numbers_are_read_the_same_way_in_every_language()
        {
            var http = new DefaultHttpContext();
            http.Request.ContentType = "application/x-www-form-urlencoded";
            http.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["ThresholdPercent"] = "4.5" });
            var context = new ValueProviderFactoryContext(new ActionContext(http, new RouteData(), new ActionDescriptor()));

            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = AppCultures.For("pt");
                await new InvariantFormValueProviderFactory().CreateValueProviderAsync(context);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            var value = Assert.Single(context.ValueProviders).GetValue("ThresholdPercent");
            Assert.Equal("4.5", value.FirstValue);
            Assert.Equal(CultureInfo.InvariantCulture, value.Culture);
            Assert.Equal(4.5m, Convert.ToDecimal(value.FirstValue, value.Culture));
        }
    }
}

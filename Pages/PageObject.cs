using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace SiteInteressantTester.Pages {
    internal abstract class PageObject {
        internal WebDriver Driver;

        internal static Dictionary<Type,Regex> UrlRegexes = new();
        internal Regex UrlRegex { get => UrlRegexes[GetType()]; }

        public PageObject(WebDriver driver) {
            Driver = driver;
        }
    }
}

using MySqlConnector;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SiteInteressantTester.Pages;
using System.Text.RegularExpressions;

namespace SiteInteressantTester {
    internal static class GClass {
        public static HttpClientHandler HttpHandler;
        public static HttpClient Http;

        static GClass() {
            HttpHandler = new HttpClientHandler() {
                CookieContainer = new System.Net.CookieContainer()
            };
            HttpHandler.UseCookies = true;
            HttpHandler.ServerCertificateCustomValidationCallback = (_,_,_,_) => true;
            Http = new(HttpHandler);
        }

        public static MySqlConnection GetNewMySqlConnection() {
            return new($"Server={Program.DB_Host};User ID={Program.DB_Username};Password={Program.DB_Password};Database={Program.DB_Name}");
        }

        public static string GetRoot(string? subdomain = null) {
            return subdomain == null ? "https://local_siteinteressant.net" : $"https://{subdomain}.local_siteinteressant.net/";
        }

        internal static void WriteColoredLine(string s, ConsoleColor? fgColor = null, ConsoleColor? bgColor = null) {
            ConsoleColor ofg = Console.ForegroundColor;
            ConsoleColor obg = Console.BackgroundColor;
            if (fgColor != null) Console.ForegroundColor = fgColor.Value;
            if (bgColor != null) Console.BackgroundColor = bgColor.Value;
            Console.WriteLine(s);
            if (fgColor != null) Console.ForegroundColor = ofg;
            if (bgColor != null) Console.BackgroundColor = obg;
        }
    }

    internal static class DriverExt {
        public static PageObject GetPageObject(this WebDriver driver, Type? waitForPageObject = null, double waitTime = 10) {
            if (waitForPageObject != null) {
                if (!waitForPageObject.IsSubclassOf(typeof(PageObject))) throw new Exception("Bad type.");
                new WebDriverWait(driver, TimeSpan.FromSeconds(waitTime)).Until(d => PageObject.UrlRegexes[waitForPageObject].IsMatch(driver.Url));
            }

            string url = driver.Url;
            foreach (KeyValuePair<Type, Regex> entry in PageObject.UrlRegexes) if (entry.Value.IsMatch(url)) return (PageObject)Activator.CreateInstance(entry.Key,driver)!;
            throw new Exception($"No page object found for url: '{url}'.");
        }
    }

    internal static class IWebElementExt {
        public static void SetValue(this IWebElement element, string value) {
            element.Clear();
            element.SendKeys(value);
        }
    }
}

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
            Http = new(HttpHandler);
        }

        public static MySqlConnection GetNewMySqlConnection() {
            return new("Server=127.0.0.1;User ID=root;Password=LwgaHpJjWpDg7L8QR2;Database=test_siteinteressant");
        }

        public static string GetRoot(string? subdomain = null) {
            return subdomain == null ? "http://local_siteinteressant.net" : $"http://{subdomain}.local_siteinteressant.net/";
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

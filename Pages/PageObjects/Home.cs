using OpenQA.Selenium;
using SiteInteressantTester.Pages.Popup;

namespace SiteInteressantTester.Pages {
    internal class Home : PageObject {
        internal static string Url = $"{GClass.GetRoot()}/home";

        internal ConnexionForm ConnexionForm;

        static Home() {
            UrlRegexes.Add(typeof(Home),new($"^{GClass.GetRoot()}/home(?:.php)?$"));
        }

        public Home(WebDriver driver) : base(driver) {
            ConnexionForm = new ConnexionForm(driver);
        }
    }
}
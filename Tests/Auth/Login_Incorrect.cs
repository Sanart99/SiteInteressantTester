using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using SiteInteressantTester.Pages;
using SiteInteressantTester.Test;

namespace SiteInteressantTester.Tests.Auth {
    internal class Login_Incorrect : ITest {
        public bool Exec(WebDriver driver) {
            WebDriverWait wait = new(driver, TimeSpan.FromSeconds(10));

            driver.Navigate().GoToUrl(Home.Url);
            Home pageHome = (Home)driver.GetPageObject(typeof(Home));
            pageHome.ConnexionForm.EnterCredentials("UserTest1","incorrectpassword",true);
            wait.Until(d => driver.SwitchTo().Alert() != null);

            return driver.SwitchTo().Alert().Text.StartsWith("[NOT_FOUND]");
        }
    }
}

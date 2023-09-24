using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SiteInteressantTester.Pages;

namespace SiteInteressantTester.Test.Auth {
    internal class Login_Correct : ITest {
        public bool Exec(WebDriver driver) {
            WebDriverWait wait = new(driver, TimeSpan.FromSeconds(10));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

            driver.Navigate().GoToUrl(Home.Url);
            Home pageHome = (Home)driver.GetPageObject(typeof(Home));
            pageHome.ConnexionForm.EnterCredentials("UserTest1","correctpassword",true);
            wait.Until(d => driver.FindElement(By.CssSelector("#topBar")).Displayed);

            return driver.FindElement(By.CssSelector("#topBar")).GetCssValue("display") != "none";
        }
    }
}

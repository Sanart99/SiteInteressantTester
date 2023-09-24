using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using SiteInteressantTester.Pages;
using SiteInteressantTester.Test;
using MySqlConnector;

namespace SiteInteressantTester.Tests.Auth {
    internal class Login_TooManyAttempts : ITest {
        public bool Exec(WebDriver driver) {
            WebDriverWait wait = new(driver, TimeSpan.FromSeconds(10));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

            driver.Navigate().GoToUrl(Home.Url);
            Home pageHome = (Home)driver.GetPageObject(typeof(Home));

            int maxAttempts = 11;
            for (int i = 0; i < maxAttempts; i++) {
                Console.WriteLine($"Attempt number {i+1}");
                pageHome.ConnexionForm.EnterCredentials("UserTest1","incorrectpassword",true);
                wait.Until(d => driver.SwitchTo().Alert() != null);
                IAlert alert = driver.SwitchTo().Alert();
                string text = alert.Text;
                
                if (text.StartsWith("[NOT_FOUND]")) { alert.Accept(); continue; }
                else if (text.StartsWith("[PROHIBITED]")) return true;
                else { Console.WriteLine($"Unknown message: {text}"); return false; }
            }
            
            Console.WriteLine($"Max attempts ({maxAttempts}) reached.");
            return false;
        }
    }
}

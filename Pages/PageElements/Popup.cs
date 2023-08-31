using OpenQA.Selenium;

namespace SiteInteressantTester.Pages.Popup {
    internal class ConnexionForm {
        internal WebDriver Driver;
        internal string Username {
            get => Driver.FindElement(By.CssSelector("#connexionForm_connect_username")).Text;
            set => Driver.FindElement(By.CssSelector("#connexionForm_connect_username")).SetValue(value);
        }
        internal string Password {
            get => Driver.FindElement(By.CssSelector("#connexionForm_connect_password")).Text;
            set => Driver.FindElement(By.CssSelector("#connexionForm_connect_password")).SetValue(value);
        }

        public ConnexionForm(WebDriver driver) {
            Driver = driver;
        }

        public void EnterCredentials(string username, string password, bool submit=false) {
            Driver.FindElement(By.CssSelector("#connexionForm_connect_username")).SetValue(username);
            Driver.FindElement(By.CssSelector("#connexionForm_connect_password")).SetValue(password);
            if (submit) Driver.FindElement(By.CssSelector("#connexionForm_connect input[type=\"submit\"]")).Click();
        }
    }
}

using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using SiteInteressantTester.Pages;
using SiteInteressantTester.Test;

namespace SiteInteressantTester.Tests.Forum {
    internal class CreateThread_Correct : ITest {
        public bool Exec(WebDriver driver) {
            WebDriverWait wait = new(driver, TimeSpan.FromSeconds(10));
            string threadTitle = "Test Title";
            string threadContent = "Test thread's content.";

            driver.Navigate().GoToUrl(Home.Url);
            Home pageHome = (Home)driver.GetPageObject(typeof(Home));
            pageHome.ConnexionForm.EnterCredentials("UserTest1","correctpassword",true);
            wait.Until(d => driver.FindElement(By.CssSelector("#topBar_r_slideArea")).Displayed);

            new Actions(driver).MoveToElement(driver.FindElement(By.CssSelector("#topBar_r_slideArea"))).Perform();
            wait.Until(d => driver.FindElement(By.CssSelector("#rightBar_optionsDiv_forum")).Displayed);
            driver.FindElement(By.CssSelector("#rightBar_optionsDiv_forum")).Click();
            wait.Until(d => driver.FindElement(By.CssSelector("#forumL .newThreadLoader")).Displayed);
            driver.FindElement(By.CssSelector("#forumL .newThreadLoader")).Click();

            wait.Until(d => driver.FindElement(By.CssSelector("#forumR #newThread_title")).Displayed);
            driver.FindElement(By.CssSelector("#forumR #newThread_title")).SendKeys(threadTitle);
            driver.FindElement(By.CssSelector("""#forumR .replyForm textarea[name="msg"]""")).SendKeys(threadContent);
            driver.FindElement(By.CssSelector("""#forumR .replyForm input[type="submit"]""")).Click();
            wait.Until(d => driver.FindElement(By.CssSelector("""#forum_threads tbody .thread[data-selected="true"] .title p""")).Text == threadTitle);

            string displayedTitle = driver.FindElement(By.CssSelector("#forumR .forum_mainBar_sub1 p")).Text;
            return threadTitle == displayedTitle;
        }
    }
}
using MySqlConnector;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using SiteInteressantTester.Test;
using System.Reflection;
using System.Text.Json;

namespace SiteInteressantTester {
    internal class Program {
        public static void Main(string[] args) {
            if (args.Length == 0  || args[0] != "-all") {
                Console.WriteLine("This only runs with '-all' for now.");
                return;
            }

            if (!IsServerInTestMode()) { Console.WriteLine("Server isn't in test mode."); return; }
            DBInit(false);

            List<ITest> tests = new();
            IEnumerable<Type> types = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace?.StartsWith("SiteInteressantTester.Test") ?? false);
            foreach (Type t in types) {
                if (t.FullName == "SiteInteressantTester.Test.ITest") continue;
                if (t.GetInterface("ITest") != null) tests.Add((ITest)Activator.CreateInstance(t)!);
            }
            
            WebDriver driver;
            string[] driverNames = new string[] { "Chrome", "Firefox" };
            Type[] driverTypes = new Type[] { typeof(ChromeDriver), typeof(FirefoxDriver) };
            List<(string,string,string)> testResults = new();

            foreach (ITest test in tests) {
                string testName = test.GetType().Name;
                Console.WriteLine("---------------------------");
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"Testing : {testName}");
                Console.ForegroundColor = ConsoleColor.White;

                bool stopTest = false;
                for (int i=0; i<2; i++) {
                    if (!IsServerInTestMode()) { Console.WriteLine("Server isn't in test mode."); stopTest = true; break; }
                    DBInit();

                    string driverName = driverNames[i];
                    Console.WriteLine($"\n***** {driverName} *****");
                    driver = (WebDriver)Activator.CreateInstance(driverTypes[i])!;

                    bool bResult = false;
                    try { bResult = test.Exec(driver); }
                    catch (Exception e) { Console.WriteLine(e.Message); }
                     
                    string sResult = (bResult ? "SUCCESS" : "FAILURE");
                    
                    SwitchColorFromResult(sResult);
                    Console.WriteLine($"\nResult : {sResult}");
                    Console.ForegroundColor = ConsoleColor.White;

                    testResults.Add((testName,driverName,sResult));

                    driver.Quit();

                    if (!bResult) { stopTest = true; break; }
                }
                if (stopTest) break;
            }
            Console.WriteLine("---------------------------\n");

            foreach ((string testName,string driverName, string testResult)tr in testResults) {
                SwitchColorFromResult(tr.testResult);
                Console.WriteLine($"{tr.testName} | {tr.driverName} | {tr.testResult}");
            }
            Console.ForegroundColor = ConsoleColor.White;

            return;
        }

        private static void SwitchColorFromResult(string s) {
            Console.ForegroundColor = s switch {
                "SUCCESS" => ConsoleColor.Green,
                "FAILURE" => ConsoleColor.Red,
                _ => ConsoleColor.White
            };
        }

        private static bool IsServerInTestMode() {
            var res = GClass.Http.PostAsync($"{GClass.GetRoot("api")}/graphql.php", new MultipartFormDataContent() {
                { new StringContent("""{ "query": "query { testMode }" }"""), "gqlQuery" }
            });
            res.Wait();
            var res2 = res.Result.Content.ReadAsStringAsync();
            res2.Wait();
            GQLResponse? v = JsonSerializer.Deserialize<GQLResponse>(res2.Result, new JsonSerializerOptions() {PropertyNameCaseInsensitive = true});

            return v?.Data?.TestMode == true;
        }

        internal static void DBInit(bool silent = true) {
            MySqlConnection conn = GClass.GetNewMySqlConnection();
            conn.Open();
            new MySqlCommand("START TRANSACTION", conn).ExecuteNonQuery();
            new MySqlCommand("SET FOREIGN_KEY_CHECKS = 0", conn).ExecuteNonQuery();

            List<string> tableNames = new();
            MySqlDataReader r = new MySqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema='test_siteinteressant'", conn).ExecuteReader();
            while (r.Read()) tableNames.Add(r.GetString(0));
            r.Close();

            foreach (string s in tableNames) {
                if (!silent) Console.WriteLine($"Dropping '{s}'");
                new MySqlCommand($"DROP TABLE IF EXISTS {s}",conn).ExecuteNonQuery();
            }

            if (!silent) Console.WriteLine("Creating tables...");
            new MySqlCommand(@"
            CREATE TABLE `comments` (
	            `thread_id` INT(11) UNSIGNED NOT NULL,
	            `number` INT(11) UNSIGNED NOT NULL,
	            `author_id` INT(11) UNSIGNED NOT NULL,
	            `content` TEXT NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `creation_date` DATETIME NOT NULL,
	            `last_edition_date` DATETIME NULL DEFAULT NULL,
	            `read_by` LONGTEXT NOT NULL DEFAULT json_array() COLLATE 'utf8mb4_bin',
	            PRIMARY KEY (`thread_id`, `number`) USING BTREE,
	            INDEX `fk_comments__authorId` (`author_id`) USING BTREE,
	            CONSTRAINT `fk_comments__authorId` FOREIGN KEY (`author_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE ON DELETE RESTRICT,
	            CONSTRAINT `fk_comments__threadId` FOREIGN KEY (`thread_id`) REFERENCES `threads` (`id`) ON UPDATE CASCADE ON DELETE CASCADE,
	            CONSTRAINT `read_by` CHECK (json_valid(`read_by`))
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB;",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `connections` (
	            `id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	            `user_id` INT(11) UNSIGNED NOT NULL,
	            `session_id` VARCHAR(50) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `app_id` VARCHAR(500) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `created_at` DATETIME NOT NULL,
	            `last_activity_at` DATETIME NOT NULL,
	            PRIMARY KEY (`id`) USING BTREE,
	            INDEX `user_id` (`user_id`) USING BTREE,
	            CONSTRAINT `fk_user_id__users` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE ON DELETE RESTRICT
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB;",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `connection_attempts` (
	            `id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	            `user_id` INT(11) UNSIGNED NULL DEFAULT NULL,
	            `app_id` VARCHAR(500) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `remote_address` VARCHAR(45) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `date` DATETIME NOT NULL,
	            `successful` TINYINT(1) UNSIGNED NOT NULL,
	            `error_type` VARCHAR(200) NULL DEFAULT NULL COLLATE 'utf8mb4_unicode_ci',
	            PRIMARY KEY (`id`) USING BTREE,
	            INDEX `fk_connections__userId` (`user_id`) USING BTREE,
	            CONSTRAINT `fk_connections__userId` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE ON DELETE SET NULL
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `emojis` (
	            `id` VARCHAR(300) NOT NULL COLLATE 'utf8mb4_bin',
	            `aliases` LONGTEXT NOT NULL DEFAULT json_array() COLLATE 'utf8mb4_bin',
	            `consommable` TINYINT(1) NOT NULL,
	            PRIMARY KEY (`id`) USING BTREE,
	            CONSTRAINT `aliases` CHECK (json_valid(`aliases`))
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB;",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `invite_codes` (
	            `id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	            `referrer_id` INT(11) UNSIGNED NOT NULL,
	            `code` VARCHAR(200) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `max_referree_count` INT(11) UNSIGNED NOT NULL,
	            PRIMARY KEY (`id`) USING BTREE,
	            UNIQUE INDEX `uq_invite_codes__code` (`code`) USING BTREE,
	            INDEX `fk_invite_codes__users` (`referrer_id`) USING BTREE,
	            CONSTRAINT `fk_invite_codes__users` FOREIGN KEY (`referrer_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE ON DELETE RESTRICT
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB;",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `invite_queues` (
	            `id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	            `code` VARCHAR(200) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `date` DATETIME NOT NULL,
	            `session_id` VARCHAR(50) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `user_id` INT(11) UNSIGNED NULL DEFAULT NULL,
	            PRIMARY KEY (`id`) USING BTREE,
	            UNIQUE INDEX `fk_unique_session_id` (`code`, `session_id`) USING BTREE,
	            CONSTRAINT `fk_code__invite_codes` FOREIGN KEY (`code`) REFERENCES `invite_codes` (`code`) ON UPDATE CASCADE ON DELETE RESTRICT
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `notifications` (
	            `user_id` INT(11) UNSIGNED NOT NULL,
	            `number` INT(11) UNSIGNED NOT NULL,
	            `action_group` ENUM('forum') NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `action` ENUM('addThread','addComment','remComment','editComment') NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `creation_date` DATETIME NOT NULL,
	            `last_update_date` DATETIME NOT NULL,
	            `read_date` DATETIME NULL DEFAULT NULL,
	            `details` LONGTEXT NULL DEFAULT NULL COLLATE 'utf8mb4_bin',
	            `n` INT(11) UNSIGNED NULL DEFAULT NULL,
	            `record_id` INT(11) UNSIGNED NULL DEFAULT NULL,
	            PRIMARY KEY (`user_id`, `number`) USING BTREE,
	            INDEX `fk_notifications__actions` (`action_group`, `action`) USING BTREE,
	            INDEX `fk_notifications__record_id` (`record_id`) USING BTREE,
	            CONSTRAINT `fk_notifications__actions` FOREIGN KEY (`action_group`, `action`) REFERENCES `records` (`action_group`, `action`) ON UPDATE CASCADE ON DELETE NO ACTION,
	            CONSTRAINT `fk_notifications__record_id` FOREIGN KEY (`record_id`) REFERENCES `records` (`id`) ON UPDATE CASCADE ON DELETE SET NULL,
	            CONSTRAINT `details` CHECK (json_valid(`details`))
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB;",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `records` (
	            `id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	            `user_id` INT(11) UNSIGNED NOT NULL,
	            `action_group` ENUM('forum') NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `action` ENUM('addThread','addComment','remComment','editComment') NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `details` LONGTEXT NULL DEFAULT NULL COLLATE 'utf8mb4_bin',
	            `date` DATETIME NOT NULL,
	            `notified_ids` LONGTEXT NOT NULL DEFAULT json_array() COLLATE 'utf8mb4_bin',
	            PRIMARY KEY (`id`) USING BTREE,
	            INDEX `idx_actions` (`action_group`, `action`, `date`) USING BTREE,
	            CONSTRAINT `details` CHECK (json_valid(`details`)),
	            CONSTRAINT `notified_ids` CHECK (json_valid(`notified_ids`))
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB;",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `threads` (
	            `id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	            `author_id` INT(11) UNSIGNED NOT NULL,
	            `title` VARCHAR(200) NOT NULL DEFAULT '' COLLATE 'utf8mb4_unicode_ci',
	            `tags` VARCHAR(1000) NOT NULL DEFAULT '' COLLATE 'utf8mb4_unicode_ci',
	            `creation_date` DATETIME NOT NULL,
	            `last_update_date` DATETIME NOT NULL,
	            `permission` ENUM('all_users','current_users') NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `following_ids` LONGTEXT NOT NULL DEFAULT json_array() COLLATE 'utf8mb4_bin',
	            PRIMARY KEY (`id`) USING BTREE,
	            INDEX `idx_threads__last_update_date` (`last_update_date`, `id`) USING BTREE,
	            CONSTRAINT `following_ids` CHECK (json_valid(`following_ids`))
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB;",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `users` (
	            `id` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	            `titles` SET('Administrator','oldInteressant') NOT NULL DEFAULT '' COLLATE 'utf8mb4_unicode_ci',
	            `name` VARCHAR(50) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `password` VARCHAR(80) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `associatedIDs` VARCHAR(100) NULL DEFAULT NULL COLLATE 'utf8mb4_unicode_ci',
	            `registration_date` DATETIME NOT NULL,
	            `avatar_name` VARCHAR(75) NULL DEFAULT NULL COLLATE 'utf8mb4_unicode_ci',
	            `settings` LONGTEXT NOT NULL DEFAULT '{}' COLLATE 'utf8mb4_bin',
	            PRIMARY KEY (`id`) USING BTREE,
	            UNIQUE INDEX `idx_unique_name` (`name`) USING BTREE
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB",conn).ExecuteNonQuery();
            new MySqlCommand(@"
            CREATE TABLE `users_emojis` (
	            `user_id` INT(11) UNSIGNED NOT NULL,
	            `emoji_id` VARCHAR(300) NOT NULL COLLATE 'utf8mb4_unicode_ci',
	            `amount` INT(11) UNSIGNED NULL DEFAULT NULL,
	            PRIMARY KEY (`user_id`, `emoji_id`) USING BTREE,
	            CONSTRAINT `fk_users_emojis__id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE ON DELETE CASCADE
            )
            COLLATE='utf8mb4_unicode_ci'
            ENGINE=InnoDB;",conn).ExecuteNonQuery();

            if (!silent) Console.WriteLine("Populating 'users'...");
            new MySqlCommand(@"
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (1, 'Administrator,oldInteressant', 'UserTest1', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 17:26:28', NULL, '{}');
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (2, 'oldInteressant', 'UserTest2', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 18:05:13', NULL, '{}');
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (3, 'oldInteressant', 'UserTest3', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 18:09:46', NULL, '{}');
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (4, '', 'UserTest4', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 18:24:06', NULL, '{}');
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (5, '', 'UserTest5', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 18:31:59', NULL, '{}');
            ",conn).ExecuteNonQuery();

            new MySqlCommand("SET FOREIGN_KEY_CHECKS = 1", conn).ExecuteNonQuery();
            new MySqlCommand("COMMIT", conn).ExecuteNonQuery();
            if (!silent) Console.WriteLine("DB initialized.");
        }
    }
}

using MySqlConnector;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using SiteInteressantTester.Test;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SiteInteressantTester {
    internal partial class Program {
        public static bool Headless { get; private set; } = false;
        public static int BrowserWidth { get; private set; } = 1000;
        public static int BrowserHeight { get; private set; } = 1000;
        public static bool Logging { get; private set; } = false;
        public static bool All { get; private set; } = false;
        public static List<string> TestsSelector { get; private set; } = new();
        public static bool UseChrome { get; private set; } = true;
        public static bool UseFirefox { get; private set; } = true;

        public static string DB_Host { get; private set; } = "localhost";
        public static string DB_Name { get; private set; } = "";
        public static string DB_Username { get; private set; } = "root";
        public static string DB_Password { get; private set; } = "";

        [GeneratedRegex(@"^-tests=(\^?(?>\w+)\$?(?:,\^?(?>\w+)\$?)*)$")]
        private static partial Regex RegexArgTests();
        [GeneratedRegex(@"^-browsers=((?:chrome|firefox)(?:,(?:chrome|firefox))*)$")]
        private static partial Regex RegexArgBrowsers();

        public static int Main(string[] args) {
            foreach (string arg in args) {
                switch (arg) {
                    case "-all":
                        All = true;
                        break;
                    case "-headless":
                        Headless = true;
                        break;
                    case "-log":
                        Logging = true;
                        break;
                    case var _ when new Regex(@"^--db-host").IsMatch(arg):
                        Match mDbHost = new Regex(@"^--db-host=([^\s;]+)$").Match(arg);
                        if (!mDbHost.Success) { Console.WriteLine("Invalid --db-host parameter."); return 1; }
                        DB_Host = mDbHost.Groups[1].Value;
                        break;
                    case var _ when new Regex(@"^--db-name").IsMatch(arg):
                        Match mDbName = new Regex(@"^--db-name=([^\s;]+)$").Match(arg);
                        if (!mDbName.Success) { Console.WriteLine("Invalid --db-name parameter."); return 1; }
                        DB_Name = mDbName.Groups[1].Value;
                        break;
                    case var _ when new Regex(@"^--db-username").IsMatch(arg):
                        Match mDbUsername = new Regex(@"^--db-username=([^\s;]+)$").Match(arg);
                        if (!mDbUsername.Success) { Console.WriteLine("Invalid --db-username parameter."); return 1; }
                        DB_Username = mDbUsername.Groups[1].Value;
                        break;
                    case var _ when new Regex(@"^--db-password").IsMatch(arg):
                        Match mDbPassword = new Regex(@"^--db-password=([^\s;]+)$").Match(arg);
                        if (!mDbPassword.Success) { Console.WriteLine("Invalid --db-password parameter."); return 1; }
                        DB_Password = mDbPassword.Groups[1].Value;
                        break;
                    case var _ when new Regex(@"^-width=\d+$").IsMatch(arg):
                        BrowserWidth = int.Parse(new Regex(@"^-width=(\d+)$").Match(arg).Groups[1].Value);
                        break;
                    case var _ when new Regex(@"^-height=\d+$").IsMatch(arg):
                        BrowserHeight = int.Parse(new Regex(@"^-height=(\d+)$").Match(arg).Groups[1].Value);
                        break;
                    case var _ when RegexArgTests().IsMatch(arg):
                        TestsSelector = new List<string>(RegexArgTests().Match(arg).Groups[1].Value.Split(','));
                        break;
                    case var _ when new Regex(@"^-browsers").IsMatch(arg):
                        if (!RegexArgBrowsers().IsMatch(arg)) { Console.WriteLine("Invalid -browsers parameter."); return 1; }

                        UseChrome = UseFirefox = false;
                        foreach (string s in RegexArgBrowsers().Match(arg).Groups[1].Value.Split(',')) {
                            if (s == "firefox") { UseFirefox = true; continue; }
                            else if (s == "chrome") { UseChrome = true; continue; }
                        };

                        if (UseChrome == false && UseFirefox == false) { Console.WriteLine("Invalid -browsers parameter."); return 1; }
                        break;
                }
            }
            if (DB_Name == "") {
                GClass.WriteColoredLine("Specify a database name to use for testing. (use --db-name)",ConsoleColor.DarkRed);
                return 1;
            }

            List<string> chromeArgs = new();
            if (UseChrome) {
                if (Headless) chromeArgs.Add("--headless");
                chromeArgs.Add($"--window-size={BrowserWidth},{BrowserHeight}");
            }

            List<string> firefoxArgs = new();
            if (UseFirefox) {
                if (Headless) firefoxArgs.Add("-headless");
                firefoxArgs.Add($"-width={BrowserWidth}");
                firefoxArgs.Add($"-height={BrowserHeight}");
            }

            List<ITest> tests = new();
            IEnumerable<Type> types = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace?.StartsWith("SiteInteressantTester.Test") ?? false);
            if (All) {
                foreach (Type t in types) {
                    if (t.FullName == "SiteInteressantTester.Test.ITest") continue;
                    if (t.GetInterface("ITest") != null) tests.Add((ITest)Activator.CreateInstance(t)!);
                }
            } else if (TestsSelector.Count > 0) {
                foreach (Type t in types) {
                    if (t.FullName == "SiteInteressantTester.Test.ITest") continue;
                    if (t.GetInterface("ITest") != null) foreach (string sel in TestsSelector) if (sel.Length > 0) {
                        bool a = sel[0] == '^';
                        bool b = sel[sel.Length-1] == '$';
                        if (a && b) { if (t.Name == sel) tests.Add((ITest)Activator.CreateInstance(t)!); }
                        else if (a) { if (t.Name.StartsWith(sel[1..])) tests.Add((ITest)Activator.CreateInstance(t)!); }
                        else if (b) { if (t.Name.EndsWith(sel[..^1])) tests.Add((ITest)Activator.CreateInstance(t)!); }
                        else if (t.Name.Contains(sel)) tests.Add((ITest)Activator.CreateInstance(t)!);
                    }
                }
                if (tests.Count == 0) {
                    GClass.WriteColoredLine("No tests found matching the parameters.", ConsoleColor.Red);
                    return 1;
                }
                GClass.WriteColoredLine("Chosen tests: " + string.Join(", ", tests.Select(t => t.GetType().Name)) + "\n", ConsoleColor.Red);
            } else {
                Console.WriteLine("Can't use -all with -tests");
                return 1;
            }

            if (!API.IsServerInTestMode()) { Console.WriteLine("Server isn't in test mode."); return 1; }
            if (!DBInit(true)) return 1;
            
            WebDriver driver;
            List<string> driverNames = new();
            List<Type> driverTypes = new();
            if (UseChrome) { driverNames.Add("Chrome"); driverTypes.Add(typeof(ChromeDriver)); }
            if (UseFirefox) { driverNames.Add("Firefox"); driverTypes.Add(typeof(FirefoxDriver)); }

            string sNow = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff");
            string logDir = $"logs/chrome/${sNow}";
            if (Logging) Directory.CreateDirectory(logDir);

            List<(string,string,string)> testResults = new();
            bool errorEncountered = false;
            foreach (ITest test in tests) {
                string testName = test.GetType().Name;
                Console.WriteLine("\n---------------------------");
                GClass.WriteColoredLine($"Testing : {testName}\n", ConsoleColor.Magenta);

                for (int i=0; i<driverTypes.Count; i++) {
                    if (!API.IsServerInTestMode()) { Console.WriteLine("Server isn't in test mode."); errorEncountered = true; break; }
                    if (!DBInit()) return 1;

                    string driverName = driverNames[i];
                    Console.WriteLine($"\n***** {driverName} *****");
                    switch (driverTypes[i]) {
                        case var v when v == typeof(ChromeDriver):
                            ChromeOptions optChrome = new ChromeOptions();
                            optChrome.AddArguments(chromeArgs);
                            ChromeDriverService chromeSvc = ChromeDriverService.CreateDefaultService();
                            if (Logging) chromeSvc.LogPath = $"{logDir}/{testName}.txt";
                            driver = (ChromeDriver)Activator.CreateInstance(driverTypes[i],new object[] { chromeSvc, optChrome })!;
                            break;
                        case var v when v == typeof(FirefoxDriver):
                            FirefoxOptions optFirefox = new FirefoxOptions();
                            optFirefox.AddArguments(firefoxArgs);
                            if (Logging) optFirefox.LogLevel = FirefoxDriverLogLevel.Trace;
                            driver = (FirefoxDriver)Activator.CreateInstance(driverTypes[i],new object[] { optFirefox })!;
                            break;
                        default: throw new Exception("Unknown driver.");
                    }

                    bool bResult = false;
                    try { bResult = test.Exec(driver); }
                    catch (Exception e) { Console.WriteLine(e.Message); }
                     
                    string sResult = (bResult ? "SUCCESS" : "FAILURE");
                    
                    SwitchColorFromResult(sResult);
                    Console.WriteLine($"\nResult : {sResult}\n");
                    Console.ForegroundColor = ConsoleColor.White;

                    testResults.Add((testName,driverName,sResult));

                    driver.Quit();

                    if (!bResult) { errorEncountered = true; break; }
                }
                if (errorEncountered) break;
            }
            Console.WriteLine("\n---------------------------\n");

            foreach ((string testName,string driverName, string testResult)tr in testResults) {
                SwitchColorFromResult(tr.testResult);
                Console.WriteLine($"{tr.testName} | {tr.driverName} | {tr.testResult}");
            }
            Console.ForegroundColor = ConsoleColor.White;

            return errorEncountered ? 1 : 0;
        }

        private static void SwitchColorFromResult(string s) {
            Console.ForegroundColor = s switch {
                "SUCCESS" => ConsoleColor.Green,
                "FAILURE" => ConsoleColor.Red,
                _ => ConsoleColor.White
            };
        }

        internal static bool DBInit(bool verbose = false) {
            static void Log(string s) { GClass.WriteColoredLine($"[DB] {s}",ConsoleColor.Cyan); }

            Log("Initializing database...");
            MySqlConnection conn = GClass.GetNewMySqlConnection();
            try { conn.Open(); } catch {
                GClass.WriteColoredLine("Couldn't establish connection to database.",ConsoleColor.DarkRed);
                return false;
            }

            new MySqlCommand("START TRANSACTION", conn).ExecuteNonQuery();
            new MySqlCommand("SET FOREIGN_KEY_CHECKS = 0", conn).ExecuteNonQuery();

            List<string> tableNames = new();
            MySqlDataReader r = new MySqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema='test_siteinteressant'", conn).ExecuteReader();
            while (r.Read()) tableNames.Add(r.GetString(0));
            r.Close();

            foreach (string s in tableNames) {
                if (verbose) Log($"Dropping '{s}'");
                new MySqlCommand($"DROP TABLE IF EXISTS {s}",conn).ExecuteNonQuery();
            }

            if (verbose) Log("Creating tables...");
            new MySqlCommand(@"
            CREATE TABLE IF NOT EXISTS `comments` (
                `thread_id` int(11) unsigned NOT NULL,
                `number` int(11) unsigned NOT NULL,
                `author_id` int(11) unsigned NOT NULL,
                `content` text NOT NULL,
                `creation_date` datetime NOT NULL,
                `last_edition_date` datetime DEFAULT NULL,
                `read_by` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL DEFAULT json_array(),
                PRIMARY KEY (`thread_id`,`number`),
                KEY `fk_comments__authorId` (`author_id`),
                FULLTEXT KEY `idx_ft_content` (`content`),
                CONSTRAINT `fk_comments__authorId` FOREIGN KEY (`author_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE,
                CONSTRAINT `fk_comments__threadId` FOREIGN KEY (`thread_id`) REFERENCES `threads` (`id`) ON DELETE CASCADE ON UPDATE CASCADE,
                CONSTRAINT `read_by` CHECK (json_valid(`read_by`))
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `connections` (
                `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
                `user_id` int(11) unsigned NOT NULL,
                `session_id` varchar(50) NOT NULL,
                `app_id` varchar(500) NOT NULL,
                `created_at` datetime NOT NULL,
                `last_activity_at` datetime NOT NULL,
                PRIMARY KEY (`id`),
                KEY `user_id` (`user_id`),
                CONSTRAINT `fk_user_id__users` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE
            ) ENGINE=InnoDB AUTO_INCREMENT=166 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `connection_attempts` (
                `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
                `user_id` int(11) unsigned DEFAULT NULL,
                `app_id` varchar(500) NOT NULL,
                `remote_address` varchar(45) NOT NULL,
                `date` datetime NOT NULL,
                `successful` tinyint(1) unsigned NOT NULL,
                `error_type` varchar(200) DEFAULT NULL,
                PRIMARY KEY (`id`),
                KEY `fk_connections__userId` (`user_id`),
                CONSTRAINT `fk_connections__userId` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
            ) ENGINE=InnoDB AUTO_INCREMENT=226 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `emojis` (
                `id` varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
                `aliases` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL DEFAULT json_array() CHECK (json_valid(`aliases`)),
                `consommable` tinyint(1) NOT NULL,
                PRIMARY KEY (`id`) USING BTREE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `id_links` (
                `id_a` int(11) unsigned NOT NULL,
                `id_b` int(11) unsigned NOT NULL,
                PRIMARY KEY (`id_a`),
                KEY `fk_id_b__users` (`id_b`),
                CONSTRAINT `fk_id_b__users` FOREIGN KEY (`id_b`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `invite_codes` (
                `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
                `referrer_id` int(11) unsigned NOT NULL,
                `code` varchar(200) NOT NULL,
                `max_referree_count` int(11) unsigned NOT NULL,
                PRIMARY KEY (`id`) USING BTREE,
                UNIQUE KEY `uq_invite_codes__code` (`code`) USING BTREE,
                KEY `fk_invite_codes__users` (`referrer_id`),
                CONSTRAINT `fk_invite_codes__users` FOREIGN KEY (`referrer_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE
            ) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `invite_queues` (
                `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
                `code` varchar(200) NOT NULL,
                `date` datetime NOT NULL,
                `session_id` varchar(50) NOT NULL,
                `user_id` int(11) unsigned DEFAULT NULL,
                PRIMARY KEY (`id`),
                UNIQUE KEY `fk_unique_session_id` (`code`,`session_id`) USING BTREE,
                CONSTRAINT `fk_code__invite_codes` FOREIGN KEY (`code`) REFERENCES `invite_codes` (`code`) ON UPDATE CASCADE
            ) ENGINE=InnoDB AUTO_INCREMENT=11 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `kubed_comments` (
                `user_id` int(11) unsigned NOT NULL,
                `thread_id` int(11) unsigned NOT NULL,
                `comment_id` int(11) unsigned NOT NULL,
                `date` datetime NOT NULL,
                KEY `fk_kubed_comments__threadId` (`thread_id`),
                KEY `fk_kubed_comments__commentId` (`user_id`),
                CONSTRAINT `fk_kubed_comments__commentId` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON UPDATE CASCADE,
                CONSTRAINT `fk_kubed_comments__threadId` FOREIGN KEY (`thread_id`) REFERENCES `threads` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `kubed_threads` (
                `thread_id` int(11) unsigned NOT NULL,
                `user_id` int(11) unsigned NOT NULL,
                `date` datetime NOT NULL,
                PRIMARY KEY (`thread_id`,`user_id`) USING BTREE,
                KEY `fk_kubed_threads__userId` (`user_id`),
                CONSTRAINT `fk_kubed_threads__thrId` FOREIGN KEY (`thread_id`) REFERENCES `threads` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION,
                CONSTRAINT `fk_kubed_threads__userId` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `notifications` (
                `user_id` int(11) unsigned NOT NULL,
                `number` int(11) unsigned NOT NULL,
                `action_group` enum('forum') NOT NULL,
                `action` enum('addThread','addComment','remComment','remThread','editComment','kubeThread','kubeComment','unkubeThread','unkubeComment') NOT NULL,
                `creation_date` datetime NOT NULL,
                `last_update_date` datetime NOT NULL,
                `read_date` datetime DEFAULT NULL,
                `details` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`details`)),
                `n` int(11) unsigned DEFAULT NULL,
                `record_id` int(11) unsigned DEFAULT NULL,
                PRIMARY KEY (`user_id`,`number`) USING BTREE,
                KEY `fk_notifications__actions` (`action_group`,`action`),
                KEY `fk_notifications__record_id` (`record_id`),
                CONSTRAINT `fk_notifications__actions` FOREIGN KEY (`action_group`, `action`) REFERENCES `records` (`action_group`, `action`) ON DELETE NO ACTION ON UPDATE CASCADE,
                CONSTRAINT `fk_notifications__record_id` FOREIGN KEY (`record_id`) REFERENCES `records` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `records` (
                `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
                `user_id` int(11) unsigned NOT NULL,
                `action_group` enum('forum') NOT NULL,
                `action` enum('addThread','addComment','remComment','remThread','editComment','kubeThread','kubeComment','unkubeThread','unkubeComment') NOT NULL,
                `details` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL CHECK (json_valid(`details`)),
                `date` datetime NOT NULL,
                `notified_ids` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL DEFAULT json_array(),
                PRIMARY KEY (`id`),
                KEY `idx_actions` (`action_group`,`action`,`date`) USING BTREE,
                CONSTRAINT `notified_ids` CHECK (json_valid(`notified_ids`))
            ) ENGINE=InnoDB AUTO_INCREMENT=374 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `threads` (
                `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
                `author_id` int(11) unsigned NOT NULL,
                `title` varchar(200) NOT NULL DEFAULT '',
                `tags` varchar(1000) NOT NULL DEFAULT '',
                `creation_date` datetime NOT NULL,
                `last_update_date` datetime NOT NULL,
                `permission` enum('all_users','current_users') NOT NULL,
                `following_ids` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL DEFAULT json_array() CHECK (json_valid(`following_ids`)),
                PRIMARY KEY (`id`),
                KEY `idx_threads__last_update_date` (`last_update_date`,`id`) USING BTREE
            ) ENGINE=InnoDB AUTO_INCREMENT=49 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `tid_activities` (
                `user_id` int(11) unsigned NOT NULL,
                `date` date NOT NULL,
                `thread_count` int(11) unsigned NOT NULL,
                `comment_count` int(11) unsigned NOT NULL,
                PRIMARY KEY (`user_id`,`date`) USING BTREE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `tid_comments` (
                `thread_id` int(11) unsigned NOT NULL,
                `id` int(11) unsigned NOT NULL,
                `author_id` int(11) unsigned DEFAULT NULL,
                `states` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT json_array() CHECK (json_valid(`states`)),
                `content` mediumtext NOT NULL,
                `content_warning` text DEFAULT NULL,
                `displayed_date` varchar(50) NOT NULL,
                `deduced_date` date NOT NULL,
                `load_timestamp` bigint(20) unsigned NOT NULL,
                PRIMARY KEY (`thread_id`,`id`),
                KEY `idx_deduced_date` (`deduced_date`) USING BTREE,
                FULLTEXT KEY `idx_ft_content` (`content`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `tid_threads` (
                `id` int(11) unsigned NOT NULL,
                `author_id` int(11) unsigned NOT NULL,
                `title` varchar(400) NOT NULL,
                `created_at` date NOT NULL,
                `minor_tag` varchar(100) DEFAULT NULL,
                `major_tag` varchar(100) DEFAULT NULL,
                `states` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT json_array(),
                `kube_count` smallint(5) unsigned NOT NULL,
                `page_count` tinyint(3) unsigned NOT NULL,
                `comment_count` smallint(5) unsigned NOT NULL,
                PRIMARY KEY (`id`),
                KEY `idx_created_at` (`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `tid_users` (
                `id` int(11) unsigned NOT NULL,
                `name` varchar(100) NOT NULL,
                PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `users` (
                `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
                `titles` set('Administrator','oldInteressant') NOT NULL DEFAULT '',
                `name` varchar(50) NOT NULL,
                `password` varchar(80) NOT NULL,
                `associatedIDs` varchar(100) DEFAULT NULL,
                `registration_date` datetime NOT NULL,
                `avatar_name` varchar(75) DEFAULT NULL,
                `settings` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL DEFAULT '{}',
                PRIMARY KEY (`id`),
                UNIQUE KEY `idx_unique_name` (`name`)
            ) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `users_emojis` (
                `user_id` int(11) unsigned NOT NULL,
                `emoji_id` varchar(300) NOT NULL,
                `amount` int(11) unsigned DEFAULT NULL,
                PRIMARY KEY (`user_id`,`emoji_id`) USING BTREE,
                CONSTRAINT `fk_users_emojis__id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            CREATE TABLE IF NOT EXISTS `user_stats` (
                `type` int(11) NOT NULL,
                `user_id` int(11) unsigned NOT NULL,
                `activities_last_full_refresh` datetime NOT NULL,
                PRIMARY KEY (`type`,`user_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",conn).ExecuteNonQuery();

            if (verbose) Log("Populating 'users'...");
            new MySqlCommand(@"
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (1, 'Administrator,oldInteressant', 'UserTest1', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 17:26:28', NULL, '{}');
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (2, 'oldInteressant', 'UserTest2', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 18:05:13', NULL, '{}');
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (3, 'oldInteressant', 'UserTest3', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 18:09:46', NULL, '{}');
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (4, '', 'UserTest4', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 18:24:06', NULL, '{}');
                INSERT INTO `users` (`id`, `titles`, `name`, `password`, `associatedIDs`, `registration_date`, `avatar_name`, `settings`) VALUES (5, '', 'UserTest5', '.iXNOWA04NX4fLklQcJnkYQFfLhUos1.', NULL, '2023-08-01 18:31:59', NULL, '{}');
            ",conn).ExecuteNonQuery();
            API.LoginUser("UserTest5","correctpassword");
            API.CreateThread("plop", Array.Empty<string>(), "plplplplpll");
            API.LogoutUser();

            new MySqlCommand("SET FOREIGN_KEY_CHECKS = 1", conn).ExecuteNonQuery();
            new MySqlCommand("COMMIT", conn).ExecuteNonQuery();
            Log("DB initialized.");
            return true;
        }
    }
}

using System.Text.Json;

namespace SiteInteressantTester {
    internal interface IOperation {
        public bool? Success { get; set; }
        public string? ResultCode { get; set; }
        public string? ResultMessage { get; set; }
    }

    internal class GQLResponse {
        public GQLData? Data { get; set; }
        public double? Cost { get; set; }
    }

    internal class GQLData {
        public bool? TestMode { get; set; }
        public GQLOnThreadOperation? Forum_NewThread { get; set; }
        public GQLOnRegisteredUserOperation? LoginUser { get; set; }
        public GQLSimpleOperation? LogoutUser { get; set; }
    }

    internal class GQLSimpleOperation : IOperation {
        public bool? Success { get; set; }
        public string? ResultCode { get; set; }
        public string? ResultMessage { get; set; }
    }

    internal class GQLOnThreadOperation : IOperation {
        public bool? Success { get; set; }
        public string? ResultCode { get; set; }
        public string? ResultMessage { get; set; }
        public GQLThread? Thread { get; set; }
    }

    internal class GQLOnRegisteredUserOperation : IOperation {
        public bool? Success { get; set; }
        public string? ResultCode { get; set; }
        public string? ResultMessage { get; set; }
        public GQLRegisteredUser? RegisteredUser { get; set; }
    }

    internal class GQLThread {
        public int? DbId { get; set; }
        public int[]? FollowingIds { get; set; }
    }

    internal class GQLRegisteredUser {
        public string? Id { get; set; }
        public int? DbId { get; set; }
        public string? Name { get; set; }
        public string? AvatarURL { get; set; }
    }

    internal class GQLTestMode {
        public bool? TestMode { get; set; }
    }

    class API {
        public static Dictionary<string,string> Cookies = new();
        public static bool Silent = false;

        #region API Calls
        public static GQLResponse? LoginUser(string username, string password) {
            Log($"Login User: {username} - {password}");
            string query = @"mutation LoginUser($username:String!, $password:String!) {
	            loginUser(username:$username, password:$password) {
		            __typename
		            success
		            resultCode
		            resultMessage
		            registeredUser {
			            __typename
			            id
			            name
		            }
	            }
            }";
            string variables = Newtonsoft.Json.JsonConvert.SerializeObject(new { username, password });
            return SendQuery(query, variables);
        }

        public static GQLResponse? LogoutUser() {
            Log("Logout User");
            string query = @"mutation LogoutUser {
	            logoutUser {
		            __typename
		            success
		            resultCode
		            resultMessage
	            }
            }";
            return SendQuery(query);
        }

        public static GQLResponse? CreateThread(string title, string[] tags, string content) {
            Log($"Create thread: '{title}'");
            string query = @"mutation NewThread($title:String!, $tags:[String!]!, $content:String!) {
                forum_newThread(title:$title,tags:$tags,content:$content) {
		            success
		            resultCode
		            resultMessage
		            thread {
			            dbId
			            followingIds
		            }
	            }
            }";
            string variables = Newtonsoft.Json.JsonConvert.SerializeObject(new { title, tags, content });
            return SendQuery(query,variables);
        }

        public static bool IsServerInTestMode() {
            Log("TestMode check");
            return SendQuery("query { testMode }")?.Data?.TestMode == true;
        }
        #endregion

        #region QueryConstructors
        public static GQLResponse? SendQuery(string query, string? variables = null) {
            var res = GClass.Http.PostAsync($"{GClass.GetRoot("api")}/graphql.php", GetQueryFormDataContent(query,variables));
            res.Wait();
            var res2 = res.Result.Content.ReadAsStringAsync();
            res2.Wait();
            return JsonSerializer.Deserialize<GQLResponse>(res2.Result, new JsonSerializerOptions() {PropertyNameCaseInsensitive = true});
        }

        public static MultipartFormDataContent GetQueryFormDataContent(string query, string? variables = null) {
            query = query.Replace(Environment.NewLine, "\\n").Replace("\t","\\t");
            return new MultipartFormDataContent() {
                { new StringContent($"{{ \"query\": \"{query}\" }}"), "gqlQuery" },
                { new StringContent(variables??"null"), "gqlVariables" }
            };
        }
        #endregion

        private static void Log(string s) {
            if (!Silent) GClass.WriteColoredLine($"[API] {s}",ConsoleColor.Blue);
        }
    }
}

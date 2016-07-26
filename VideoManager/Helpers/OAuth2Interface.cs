//
//	Last mod:	26 July 2016 23:04:06
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json;

namespace VideoManager.Helpers
	{
	internal class OAuth2Interface
		{
		string clientId;
		string clientSecret;
		string authorizationEndpoint;
		string tokenEndpoint;

		public class Installed
			{
			public string client_id { get; set; }
			public string client_secret { get; set; }
			public string auth_uri { get; set; }
			public string token_uri { get; set; }
			public string project_id { get; set; }
			}

		public class ClientDetails
			{
			public Installed installed { get; set; }
			}

		public Result InitialiseClient(string json)
			{
			Result result = new Result();

			try
				{
				ClientDetails clientDetails = JsonConvert.DeserializeObject<ClientDetails>(json);
				clientId = clientDetails.installed.client_id;
				clientSecret = clientDetails.installed.client_secret;
				authorizationEndpoint = clientDetails.installed.auth_uri;
				tokenEndpoint = clientDetails.installed.token_uri;
				ProjectId = clientDetails.installed.project_id;
				result.SetSuccess();
				}
			catch (Exception ex)
				{
				result.SetError(ex.Message);
				}
			return result;
			}

		public string AuthorisationCode { get; private set; }

		public string AccessToken { get; private set; }

		public string RefreshToken { get; private set; }

		public UserCredential Credential { get; private set; }

		public string ProjectId { get; private set; }

		public async Task<Result> Authorise(string[] scopes)
			{
			Result result = new Result();

			Credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
				new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
				scopes, "user", CancellationToken.None);

			if (Credential.Token.IsExpired(Credential.Flow.Clock) && ! await Credential.RefreshTokenAsync(CancellationToken.None))
				{
				result.SetError("The access token has expired but we can't refresh it :(");
				}
			else
				{
				result.SetSuccess();
				}
			return result;
			}

		public async Task<Result> Authorise(string scopes)
			{
			Result result = new Result();

			// Generates state and PKCE values.
			string state = randomDataBase64url(32);
			string code_verifier = randomDataBase64url(32);
			string code_challenge = base64urlencodeNoPadding(sha256(code_verifier));
			const string code_challenge_method = "S256";

			// Creates a redirect URI using an available port on the loopback address.
			string redirectURI = string.Format("http://{0}:{1}/", IPAddress.Loopback, GetRandomUnusedPort());

			// Creates an HttpListener to listen for requests on that redirect URI.
			var http = new HttpListener();
			http.Prefixes.Add(redirectURI);
			http.Start();

			// Creates the OAuth 2.0 authorization request.
			string authorizationRequest = string.Format("{0}?response_type=code&scope={1}&redirect_uri={2}&client_id={3}&state={4}&code_challenge={5}&code_challenge_method={6}",
					authorizationEndpoint, Uri.EscapeDataString(scopes), Uri.EscapeDataString(redirectURI), clientId, state, code_challenge, code_challenge_method);

			// Opens request in the browser.
			System.Diagnostics.Process.Start(authorizationRequest);

			// Waits for the OAuth authorization response.
			var context = await http.GetContextAsync();

			// Brings this app back to the foreground.
			Application.Current.MainWindow.Activate();

			// Sends an HTTP response to the browser.
			var response = context.Response;
			string responseString = string.Format("<html><head></head><body>You may now close this window or tab and return to the app.</body></html>");
			var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
			response.ContentLength64 = buffer.Length;
			var responseOutput = response.OutputStream;
			Task responseTask = responseOutput.WriteAsync(buffer, 0, buffer.Length).ContinueWith((task) =>
			{
				responseOutput.Close();
				http.Stop();
				Console.WriteLine("HTTP server stopped.");
			});

			// Checks for errors.
			if (context.Request.QueryString.Get("error") != null)
				{
				result.SetError($"OAuth authorization error: {context.Request.QueryString.Get("error")}.");
				}
			else if (context.Request.QueryString.Get("code") == null || context.Request.QueryString.Get("state") == null)
				{
				result.SetError("Malformed authorization response. " + context.Request.QueryString);
				}
			else
				{
				var code = context.Request.QueryString.Get("code");
				var incoming_state = context.Request.QueryString.Get("state");

				// Compares the receieved state to the expected value, to ensure that
				// this app made the request which resulted in authorization.
				if (incoming_state != state)
					{
					result.SetError($"Received request with invalid state ({incoming_state})");
					}
				else
					{
					AuthorisationCode = code;
					result = await GetTokens(code, code_verifier, redirectURI);
					}
				}
			return result;
			}

		private async Task<Result> GetTokens(string code, string code_verifier, string redirectURI)
			{
			Result result = new Result();

			string tokenRequestBody = string.Format("code={0}&redirect_uri={1}&client_id={2}&code_verifier={3}&client_secret={4}&scope=&grant_type=authorization_code",
					code, Uri.EscapeDataString(redirectURI), clientId, code_verifier, clientSecret);

			HttpWebRequest tokenRequest = (HttpWebRequest)WebRequest.Create(tokenEndpoint);
			tokenRequest.Method = "POST";
			tokenRequest.ContentType = "application/x-www-form-urlencoded";
			tokenRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
			byte[] byteVersion = Encoding.ASCII.GetBytes(tokenRequestBody);
			tokenRequest.ContentLength = byteVersion.Length;
			Stream stream = tokenRequest.GetRequestStream();
			await stream.WriteAsync(byteVersion, 0, byteVersion.Length);
			stream.Close();

			try
				{
				WebResponse tokenResponse = await tokenRequest.GetResponseAsync();
				using (StreamReader reader = new StreamReader(tokenResponse.GetResponseStream()))
					{
					string responseText = await reader.ReadToEndAsync();
					Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);

					AccessToken = tokenEndpointDecoded["access_token"];
					RefreshToken = tokenEndpointDecoded["refresh_token"];
					result.SetSuccess();
					}
				}
			catch (WebException ex)
				{
				if (ex.Status == WebExceptionStatus.ProtocolError)
					{
					var response = ex.Response as HttpWebResponse;
					if (response != null)
						{
						result.SetError("HTTP: " + response.StatusCode);
						using (StreamReader reader = new StreamReader(response.GetResponseStream()))
							{
							// reads response body
							string responseText = await reader.ReadToEndAsync();
							result.SetError(result.Error + " - " + responseText);
							}
						}
					}
				}
			return result;
			}

		/// <summary>
		/// Returns URI-safe data with a given input length.
		/// </summary>
		/// <param name="length">Input length (nb. output will be longer)</param>
		/// <returns></returns>
		public static string randomDataBase64url(uint length)
			{
			RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
			byte[] bytes = new byte[length];
			rng.GetBytes(bytes);
			return base64urlencodeNoPadding(bytes);
			}

		/// <summary>
		/// Returns the SHA256 hash of the input string.
		/// </summary>
		/// <param name="inputStirng"></param>
		/// <returns></returns>
		public static byte[] sha256(string inputStirng)
			{
			byte[] bytes = Encoding.ASCII.GetBytes(inputStirng);
			SHA256Managed sha256 = new SHA256Managed();
			return sha256.ComputeHash(bytes);
			}

		/// <summary>
		/// Base64url no-padding encodes the given input buffer.
		/// </summary>
		/// <param name="buffer"></param>
		/// <returns></returns>
		public static string base64urlencodeNoPadding(byte[] buffer)
			{
			string base64 = Convert.ToBase64String(buffer);

			// Converts base64 to base64url.
			base64 = base64.Replace("+", "-");
			base64 = base64.Replace("/", "_");
			// Strips padding.
			base64 = base64.Replace("=", "");

			return base64;
			}

		// ref http://stackoverflow.com/a/3978040
		public static int GetRandomUnusedPort()
			{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var port = ((IPEndPoint)listener.LocalEndpoint).Port;
			listener.Stop();
			return port;
			}
		}
	}

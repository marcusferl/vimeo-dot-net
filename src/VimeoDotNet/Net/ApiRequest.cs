using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VimeoDotNet.Constants;

namespace VimeoDotNet.Net
{
    /// <inheritdoc />
    public class ApiRequest : IApiRequest
    {
#if NET45
        static ApiRequest()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        }
#endif

        #region Private Fields

        /// <summary>
        /// The query string
        /// </summary>
        private readonly Dictionary<string, string> _queryString = new Dictionary<string, string>();
        /// <summary>
        /// The URL segments
        /// </summary>
        private readonly Dictionary<string, string> _urlSegments = new Dictionary<string, string>();

        /// <summary>
        /// The date format settings
        /// </summary>
        private static readonly JsonSerializerSettings DateFormatSettings = new JsonSerializerSettings
        {
            DateFormatString = "yyyy-MM-ddTHH:mm:sszzz",
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        /// <summary>
        /// The path
        /// </summary>
        private string _path;

        #endregion

        #region Public Properties

        /// <inheritdoc />
        public string ApiVersion { get; set; }
        /// <inheritdoc />
        public string ResponseType { get; set; }
        /// <inheritdoc />
        public string Protocol { get; set; }

        /// <inheritdoc />
        public string Host { get; set; }

        /// <inheritdoc />
        public int Port { get; set; }

        /// <inheritdoc />
        public HttpMethod Method { get; set; }

        /// <inheritdoc />
        public string Path
        {
            get => _path;
            set
            {
                if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) && parsed.Scheme != "file")
                {
                    Protocol = Request.MockProtocol == null ? parsed.Scheme : Request.MockProtocol;
                    Host = Request.MockHostName == null ? parsed.Host : Request.MockHostName;
                    _path = parsed.PathAndQuery;
                    Port = Request.MockPort == 0 ? parsed.Port : Request.MockPort;
                }
                else
                {
                    _path = value;
                }
            }
        }

        /// <inheritdoc />
        public HttpContent Body { get; set; }

        /// <inheritdoc />
        public IDictionary<string, string> Query => _queryString;

        /// <inheritdoc />
        public List<string> Fields { get; } = new List<string>();

        /// <inheritdoc />
        public IDictionary<string, string> UrlSegments => _urlSegments;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is add tus header.
        /// </summary>
        /// <value><c>true</c> if this instance is add tus header; otherwise, <c>false</c>.</value>
        public bool IsAddTusHeader { get; set; }

        /// <inheritdoc />
        public byte[] BinaryContent { get; set; }

        /// <inheritdoc />
        public bool ExcludeAuthorizationHeader { get; set; }

        #endregion

        #region Private Properties

        /// <summary>
        /// Rest client
        /// </summary>
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        /// <summary>
        /// Client Id
        /// </summary>
        /// <value>The client identifier.</value>
        private string ClientId { get; }

        /// <summary>
        /// Client secret
        /// </summary>
        /// <value>The client secret.</value>
        private string ClientSecret { get; }
        /// <summary>
        /// Access token
        /// </summary>
        /// <value>The access token.</value>
        private string AccessToken { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Create new request
        /// </summary>
        public ApiRequest()
        {
            Protocol = Request.MockProtocol != null ? Request.MockProtocol : Request.DefaultProtocol;
            Host = Request.MockHostName != null ? Request.MockHostName : Request.DefaultHostName;
            Port = Request.MockPort > 0 ? Request.MockPort : GetDefaultPort(Request.DefaultProtocol);
            Method = Request.DefaultMethod;
            ResponseType = ResponseTypes.Wildcard;
            ApiVersion = ApiVersions.v3_4;
            ExcludeAuthorizationHeader = false;
        }

        /// <inheritdoc />
        public ApiRequest(string clientId, string clientSecret)
            : this()
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
        }

        /// <inheritdoc />
        public ApiRequest(string accessToken)
            : this()
        {
            AccessToken = accessToken;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public async Task<IApiResponse> ExecuteRequestAsync()
        {
            var response = await Client.SendAsync(PrepareRequest()).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new ApiResponse(response.StatusCode, response.Headers, text);
        }

        /// <inheritdoc />
        public async Task<IApiResponse<T>> ExecuteRequestAsync<T>() where T : new()
        {
            var text = "";
            try
            {
                var request = PrepareRequest();
                var response = await Client.SendAsync(request).ConfigureAwait(false);
                text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return new ApiResponse<T>(response.StatusCode, response.Headers, text,
                    JsonConvert.DeserializeObject<T>(text, DateFormatSettings));
            }
            catch (Exception e)
            {
                throw new Exception($"Received error. text was {text}", e);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// PrepareRequest
        /// </summary>
        /// <returns>Rest request</returns>
        private HttpRequestMessage PrepareRequest()
        {
            SetDefaults();
            var request = new HttpRequestMessage(Method, BuildUrl());
            if (!ExcludeAuthorizationHeader)
            {
                SetAuth(request);
            }
            SetHeaders(request);
            request.Content = Body;
            return request;
        }

        /// <summary>
        /// Set request headers
        /// </summary>
        /// <param name="request">Request</param>
        private void SetHeaders(HttpRequestMessage request)
        {
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(BuildAcceptsHeader()));
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            if (IsAddTusHeader)
            {
                request.Headers.Add("Tus-Resumable", "1.0.0");
            }
        }

        /// <summary>
        /// Set authentication
        /// </summary>
        /// <param name="request">The request.</param>
        private void SetAuth(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            }
            if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
                return;
            var token = $"{ClientId}:{ClientSecret}";
            var tokenBytes = Encoding.ASCII.GetBytes(token);
            var encoded = Convert.ToBase64String(tokenBytes);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        }

        /// <summary>
        /// Set defaults
        /// </summary>
        private void SetDefaults()
        {
            Protocol = string.IsNullOrWhiteSpace(Protocol) ? Request.DefaultProtocol : Protocol;
            Host = string.IsNullOrWhiteSpace(Host) ? Request.DefaultHostName : Host;
            ResponseType = string.IsNullOrWhiteSpace(ResponseType) ? ResponseTypes.Wildcard : ResponseType;
            ApiVersion = string.IsNullOrWhiteSpace(ApiVersion) ? ApiVersions.v3_4 : ApiVersion;

            Protocol = Protocol.ToLower();
            Host = Host.ToLower();

            Port = Port > 0 ? Port : GetDefaultPort(Protocol);
        }

        /// <summary>
        /// Build accepts header
        /// </summary>
        /// <returns>Accepts header</returns>
        private string BuildAcceptsHeader()
        {
            return $"{ResponseType};{ApiVersion}";
        }

        /// <summary>
        /// Return default port
        /// </summary>
        /// <param name="protocol">Protocol</param>
        /// <returns>Port</returns>
        private static int GetDefaultPort(string protocol)
        {
            return protocol == Request.ProtocolHttps ? Request.DefaultHttpsPort : Request.DefaultHttpPort;
        }

        /// <summary>
        /// Return base URL
        /// </summary>
        /// <returns>Base URL</returns>
        private string GetBaseUrl()
        {
            var url = Protocol.ToLower() + "://";

            if (Host.EndsWith("/"))
            {
                Host = Host.Substring(0, Host.Length - 1);
            }
            url += Host;

            if (Port != GetDefaultPort(Protocol))
            {
                url += ":" + Port;
            }

            return url;
        }

        /// <summary>
        /// Build request URL
        /// </summary>
        /// <returns>Request URL</returns>
        private string BuildUrl()
        {
            var sb = new StringBuilder();
            sb.Append(GetBaseUrl());
            var path = Path;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var urlSegment in UrlSegments)
            {
                path = path.Replace($"{{{urlSegment.Key}}}", urlSegment.Value);
            }
            sb.Append(path);
            if (Fields.Count > 0)
            {
                Query.Add("fields", string.Join(",", Fields));
            }
            if (Query.Keys.Count == 0)
                return sb.ToString();
            sb.Append("?");
            sb.Append(string.Join("&", Query.Select(q => $"{q.Key}={q.Value}")));
            return sb.ToString();
        }

        #endregion
    }
}

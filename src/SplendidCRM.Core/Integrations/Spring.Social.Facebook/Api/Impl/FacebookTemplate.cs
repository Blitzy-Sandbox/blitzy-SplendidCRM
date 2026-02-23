#nullable disable
using System;
using System.Collections.Generic;
using Spring.Social.Facebook.Api;
using Spring.Social.Facebook.Api.Impl.Json;

namespace Spring.Social.Facebook.Api.Impl
{
    public abstract class AbstractOAuth2ApiBinding
    {
        protected AbstractOAuth2ApiBinding() { }
        protected AbstractOAuth2ApiBinding(string accessToken) { IsAuthorized = !string.IsNullOrEmpty(accessToken); }
        public bool IsAuthorized { get; protected set; }
        public RestTemplate RestTemplate { get; protected set; }
        protected virtual void ConfigureRestTemplate(RestTemplate restTemplate) { }
        protected virtual OAuth2Version GetOAuth2Version() { return OAuth2Version.Draft10; }
        protected virtual IList<IHttpMessageConverter> GetMessageConverters() { return new List<IHttpMessageConverter>(); }
    }

    public enum OAuth2Version { Bearer, Draft10 }
    public interface IHttpMessageConverter { }
    public class ByteArrayHttpMessageConverter : IHttpMessageConverter { }
    public class SpringJsonHttpMessageConverter : IHttpMessageConverter
    {
        public SpringJsonHttpMessageConverter(JsonMapper jsonMapper) { }
    }

    public class FacebookTemplate : AbstractOAuth2ApiBinding, IFacebook
    {
        private static readonly Uri API_URI_BASE = new Uri("https://graph.facebook.com/v2.0/");
        private string applicationNamespace;

        public FacebookTemplate() : this(null, null) { }
        public FacebookTemplate(string accessToken) : this(accessToken, null) { }
        public FacebookTemplate(string accessToken, string applicationNamespace) : base(accessToken)
        {
            this.applicationNamespace = applicationNamespace;
            this.RestTemplate = new RestTemplate();
            ConfigureRestTemplate(this.RestTemplate);
            InitSubApis();
        }

        public IUserOperations UserOperations { get; private set; }
        public IPlacesOperations PlacesOperations { get; private set; }
        public ILikeOperations LikeOperations { get; private set; }
        public IFriendOperations FriendOperations { get; private set; }
        public IFeedOperations FeedOperations { get; private set; }
        public IGroupOperations GroupOperations { get; private set; }
        public ICommentOperations CommentOperations { get; private set; }
        public IEventOperations EventOperations { get; private set; }
        public IMediaOperations MediaOperations { get; private set; }
        public IPageOperations PageOperations { get; private set; }
        public IFqlOperations FqlOperations { get; private set; }
        public IQuestionOperations QuestionOperations { get; private set; }
        public IOpenGraphOperations OpenGraphOperations { get; private set; }
        public IRestOperations RestOperations { get { return this.RestTemplate; } }

        protected override void ConfigureRestTemplate(RestTemplate restTemplate)
        {
            restTemplate.BaseAddress = API_URI_BASE;
            restTemplate.ErrorHandler = new FacebookErrorHandler();
        }

        protected override OAuth2Version GetOAuth2Version() { return OAuth2Version.Draft10; }

        private void InitSubApis()
        {
            UserOperations       = new UserTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            PlacesOperations     = new PlacesTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            FriendOperations     = new FriendTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            FeedOperations       = new FeedTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            GroupOperations      = new GroupTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            CommentOperations    = new CommentTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            LikeOperations       = new LikeTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            EventOperations      = new EventTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            MediaOperations      = new MediaTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            PageOperations       = new PageTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            FqlOperations        = new FqlTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            QuestionOperations   = new QuestionTemplate(applicationNamespace, RestTemplate, IsAuthorized);
            OpenGraphOperations  = new OpenGraphTemplate(applicationNamespace, RestTemplate, IsAuthorized);
        }
    }
}

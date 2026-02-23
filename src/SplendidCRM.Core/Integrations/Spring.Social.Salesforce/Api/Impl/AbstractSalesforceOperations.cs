#nullable disable
using System;
using System.Collections.Specialized;
using System.Text;
using Spring.Http;

namespace Spring.Social.Salesforce.Api.Impl
{
    abstract class AbstractSalesforceOperations
    {
        protected string BuildUrl(string path, NameValueCollection parameters)
        {
            StringBuilder qsBuilder = new StringBuilder();
            bool isFirst = path.IndexOf('?') < 0;
            foreach (string key in parameters)
            {
                if (isFirst) { qsBuilder.Append('?'); isFirst = false; } else { qsBuilder.Append('&'); }
                qsBuilder.Append(HttpUtils.UrlEncode(key));
                qsBuilder.Append('=');
                qsBuilder.Append(HttpUtils.UrlEncode(parameters[key]));
            }
            return path + qsBuilder.ToString();
        }
    }
}

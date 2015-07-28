using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Script.Serialization;
using System.Xml;
using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using Umbraco.Web.WebApi;

namespace OurUmbraco.Repository.Api
{
    public class SearchController : UmbracoApiController
    {
        [HttpGet]
        public HttpResponseMessage FindSimiliarItems(string types, int maxItems, string query)
        {
            query = umbraco.library.StripHtml(query);
            var keywords = string.Join(" ", GetKeywords(query)).Trim();
            var result = LuceneInContentType(keywords, types, 0, 255, maxItems);

            return new HttpResponseMessage { Content = new StringContent(result.OuterXml, Encoding.UTF8, "application/xml") };
        }

        [HttpGet]
        public string FindProjects(string query, int parent, bool wildcard)
        {
            if (query.ToLower() == "useqsstring")
                query = UmbracoContext.HttpContext.Request.QueryString["term"];

            if (wildcard && query.EndsWith("*") == false)
                query += "*";

            var searchTerm = query;
            var searcher = ExamineManager.Instance.SearchProviderCollection["MultiIndexSearcher"];

            //Search Criteria for WIKI & Projects
            var searchCriteria = searcher.CreateSearchCriteria(BooleanOperation.Or);
            var searchQuery = BuildExamineString(searchTerm, 99, "nodeName");
            searchQuery += BuildExamineString(searchTerm, 10, "description");
            searchQuery = "(" + searchQuery + ") AND +approved:1";
            var searchFilter = searchCriteria.RawQuery(searchQuery);
            IEnumerable<SearchResult> searchResults = searcher.Search(searchFilter).OrderByDescending(x => x.Score);
            searchResults = from r in searchResults
                            where r["__IndexType"] == "content" && r["nodeTypeAlias"] == "Project"
                            select r;

            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(searchResults);
        }

        public static string[] GetKeywords(string text)
        {
            string[] stop = { "about", "after", "all", "also", "an", "and", "another", "any", "are", "as", "at", "be", "because", "been", "before", "being", "between", "both", "but", "by", "came", "can", "come", "could", "did", "do", "does", "each", "else", "for", "from", "get", "got", "has", "had", "he", "have", "her", "here", "him", "himself", "his", "how", "i", "if", "in", "into", "is", "it", "its", "just", "like", "make", "many", "me", "might", "more", "most", "much", "must", "my", "never", "now", "of", "on", "only", "or", "other", "our", "out", "over", "re", "said", "same", "see", "should", "since", "so", "some", "still", "such", "take", "than", "that", "the", "their", "them", "then", "there", "these", "they", "this", "those", "through", "to", "too", "under", "up", "use", "very", "want", "was", "way", "we", "well", "were", "what", "when", "where", "which", "while", "who", "will", "with", "would", "you", "your", "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "$", "£", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };

            char[] splitChars = { ' ', '\'' };
            string[] words = text.Split(splitChars);

            var keywordCount = (from keyword in words.Except(stop)
                                group keyword by keyword into g
                                select new { Keyword = g.Key, Count = g.Count() });
            return keywordCount.OrderByDescending(k => k.Count).Select(k => k.Keyword).Take(5).ToArray();
        }

        public static string BuildExamineString(string term, int boost, string field)
        {
            var terms = term.Split(' ');
            var qs = field + ":";
            qs += "\"" + term + "\"^" + (boost + 30000).ToString() + " ";
            qs += field + ":(+" + term.Replace(" ", " +") + ")^" + (boost + 5).ToString() + " ";
            qs += field + ":(" + term + ")^" + boost.ToString() + " ";
            return qs;
        }

        public static XmlDocument LuceneInContentType(string q, string types, int currentPage, int trimAtChar, int pagesize)
        {
            int _pageSize = pagesize;
            int _page = currentPage;
            int _fragmentCharacters = trimAtChar;
            int _googleNumbers = 10;

            int _TotalResults;

            // Enable next, parent, headers and googleArrows
            DateTime searchStart = DateTime.Now;

            string queryText = q;
            string[] _fields = { "name", "content" };


            q = q.Replace("-", "\\-");

            BaseSearchProvider Searcher = ExamineManager.Instance.SearchProviderCollection["ForumSearcher"];
            var searchCriteria = Searcher.CreateSearchCriteria(BooleanOperation.Or);
            var searchQuery = BuildExamineString(queryText, 10, "Title");
            searchQuery += BuildExamineString(queryText, 7, "CommentsContent").TrimEnd(' ');

            var searchFilter = searchCriteria.RawQuery(searchQuery);

            IEnumerable<SearchResult> searchResults;

            //little hacky just to get performant searching
            if (pagesize > 0)
            {
                searchResults = Searcher.Search(searchFilter)
                .OrderByDescending(x => x.Score).Take(pagesize);
            }
            else
            {
                searchResults = Searcher.Search(searchFilter)
                .OrderByDescending(x => x.Score);
            }



            _TotalResults = searchResults.Count();
            TimeSpan searchEnd = DateTime.Now.Subtract(searchStart);
            string searchTotal = searchEnd.Seconds + ".";
            for (int i = 4; i > searchEnd.Milliseconds.ToString().Length; i--)
                searchTotal += "0";
            searchTotal += searchEnd.Milliseconds.ToString();


            // Check for paging
            int pageSize = _pageSize;
            int pageStart = _page;
            if (pageStart > 0)
                pageStart = _page * pageSize;
            int pageEnd = (_page + 1) * pageSize;



            //calculating total items and number of pages...
            int _firstGooglePage = _page - Convert.ToInt16(_googleNumbers / 2);
            if (_firstGooglePage < 0)
                _firstGooglePage = 0;
            int _lastGooglePage = _firstGooglePage + _googleNumbers;

            if (_lastGooglePage * pageSize > _TotalResults)
            {
                _lastGooglePage = (int)Math.Ceiling(_TotalResults / ((double)pageSize)); // Convert.ToInt32(hits.Length()/pageSize)+1;

                _firstGooglePage = _lastGooglePage - _googleNumbers;
                if (_firstGooglePage < 0)
                    _firstGooglePage = 0;
            }

            // Create xml document
            XmlDocument xd = new XmlDocument();
            xd.LoadXml("<search/>");
            xd.DocumentElement.AppendChild(umbraco.xmlHelper.addCDataNode(xd, "query", queryText));
            xd.DocumentElement.AppendChild(umbraco.xmlHelper.addCDataNode(xd, "luceneQuery", q));
            xd.DocumentElement.AppendChild(umbraco.xmlHelper.addTextNode(xd, "results", _TotalResults.ToString()));
            xd.DocumentElement.AppendChild(umbraco.xmlHelper.addTextNode(xd, "currentPage", _page.ToString()));
            xd.DocumentElement.AppendChild(umbraco.xmlHelper.addTextNode(xd, "totalPages", _lastGooglePage.ToString()));

            XmlNode results = umbraco.xmlHelper.addTextNode(xd, "results", "");
            xd.DocumentElement.AppendChild(results);
            
            int r = 0;
            foreach (var sr in searchResults.Skip(pageStart).Take(pageSize))
            {


                XmlNode result = xd.CreateNode(XmlNodeType.Element, "result", "");
                result.AppendChild(umbraco.xmlHelper.addTextNode(xd, "score", (sr.Score * 100).ToString()));
                result.Attributes.Append(umbraco.xmlHelper.addAttribute(xd, "resultNumber", (r + 1).ToString()));

                foreach (var field in sr.Fields)
                {
                    result.AppendChild(umbraco.xmlHelper.addTextNode(xd, field.Key, field.Value));

                }

                results.AppendChild(result);
                r++;

            }

            return xd;
        } //end search

    }
}

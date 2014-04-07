using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace PressSharp
{
    public class Blog
    {
        private static readonly XNamespace WordpressNamespace = "http://wordpress.org/export/1.2/";
        private static readonly XNamespace DublinCoreNamespace = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace RssContentNamespace = "http://purl.org/rss/1.0/modules/content/";

        private XElement channelElement;

        public string Title { get; set; }
        public string Description { get; set; }
        public IEnumerable<Author> Authors { get; set; }
        public IEnumerable<Category> Categories { get; set; }
        public IEnumerable<Tag> Tags { get; set; }

        public Blog(string xml)
            : this(XDocument.Parse(xml))
        {
        }

        public Blog(XDocument doc)
        {
            this.Authors = Enumerable.Empty<Author>();
            this.Categories = Enumerable.Empty<Category>();
            this.Tags = Enumerable.Empty<Tag>();

            this.InitializeChannelElement(doc);
            
            if (channelElement == null)
            {
                throw new XmlException("Missing channel element.");
            }

            this.Initialize();
        }

        private void InitializeChannelElement(XDocument document)
        {
            var rssRootElement = document.Root;
            if (rssRootElement == null)
            {
                throw new XmlException("No document root.");
            }

            this.channelElement = rssRootElement.Element("channel");
        }

        public void Initialize()
        {
            this.InitializeTitle();
            this.InitializeDescription();
            this.InitializeAuthors();
            this.InitializeCategories();
            this.InitializeTags();
        }

        private void InitializeTitle()
        {
            this.Title = this.GetBasicProperty("title");
        }

        private void InitializeDescription()
        {
            this.Description = this.GetBasicProperty("description");
        }

        private string GetBasicProperty(string elementName)
        {
            var element = this.channelElement.Element(elementName);
            if (element == null)
            {
                throw new XmlException(string.Format("Missing {0}.", elementName));
            }

            return element.Value;
        }

        private void InitializeAuthors()
        {
            this.Authors = this.channelElement
                .Descendants(WordpressNamespace + "author")
                .Select(ParseAuthorElement);
        }

        private static Author ParseAuthorElement(XElement authorElement)
        {
            var authorIdElement = authorElement.Element(WordpressNamespace + "author_id");
            var authorUsernameElement = authorElement.Element(WordpressNamespace + "author_login");
            var authorEmailElement = authorElement.Element(WordpressNamespace + "author_email");

            if (authorIdElement == null || authorUsernameElement == null || authorEmailElement == null)
            {
                throw new XmlException("Unable to parse malformed author.");
            }

            var author = new Author
            {
                Id = authorIdElement.Value,
                Username = authorUsernameElement.Value,
                Email = authorEmailElement.Value
            };

            return author;
        }

        private void InitializeCategories()
        {
            this.Categories = this.channelElement
                .Descendants(WordpressNamespace + "category")
                .Select(ParseCategoryElement);
        }

        private static Category ParseCategoryElement(XElement categoryElement)
        {
            var categoryIdElement = categoryElement.Element(WordpressNamespace + "term_id");
            var categoryNameElement = categoryElement.Element(WordpressNamespace + "cat_name");
            var categorySlugElement = categoryElement.Element(WordpressNamespace + "category_nicename");

            if (categoryIdElement == null || categoryNameElement == null || categorySlugElement == null)
            {
                throw new XmlException("Unable to parse malformed category.");
            }

            var category = new Category
            {
                Id = categoryIdElement.Value,
                Name = categoryNameElement.Value,
                Slug = categorySlugElement.Value
            };

            return category;
        }

        private void InitializeTags()
        {
            this.Tags = this.channelElement
                .Descendants(WordpressNamespace + "tag")
                .Select(ParseTagElement);
        }

        private static Tag ParseTagElement(XElement tagElement)
        {
            var tagIdElement = tagElement.Element(WordpressNamespace + "term_id");
            var tagSlugElement = tagElement.Element(WordpressNamespace + "tag_slug");

            if (tagIdElement == null || tagSlugElement == null)
            {
                throw new XmlException("Unable to parse malformed category.");
            }

            var tag = new Tag
            {
                Id = tagIdElement.Value,
                Slug = tagSlugElement.Value
            };

            return tag;
        }

        public IEnumerable<Post> GetPosts()
        {
            return this.channelElement
                .Elements("item")
                .Where(e => 
                    this.IsPostItem(e) && 
                    this.IsPublishedPost(e))
                .Select(ParsePostElement);
        }

        private bool IsPostItem(XElement itemElement)
        {
            if (itemElement == null)
            {
                return false;
            }

            var postTypeElement = itemElement.Element(WordpressNamespace + "post_type");
            if (postTypeElement == null)
            {
                return false;
            }

            if (postTypeElement.Value == "post")
            {
                return true;
            }

            return false;
        }

        private bool IsPublishedPost(XElement itemElement)
        {
            if (itemElement == null)
            {
                return false;
            }

            var postPublishedStatusElement = itemElement.Element(WordpressNamespace + "status");
            if(postPublishedStatusElement == null)
            {
                return false;
            }

            if (postPublishedStatusElement.Value == "publish")
            {
                return true;
            }

            return false;
        }

        private Post ParsePostElement(XElement postElement)
        {
            var postTitleElement = postElement.Element("title");
            var postUsernameElement = postElement.Element(DublinCoreNamespace + "creator");
            var postBodyElement = postElement.Element(RssContentNamespace + "encoded");
            var postPublishedAtUtcElement = postElement.Element(WordpressNamespace + "post_date_gmt");
            var postSlugElement = postElement.Element(WordpressNamespace + "post_name");

            if (postTitleElement == null ||
                postUsernameElement == null ||
                postBodyElement == null ||
                postPublishedAtUtcElement == null ||
                postSlugElement == null)
            {
                throw new XmlException("Unable to parse malformed post.");
            }

            var post = new Post
            {
                Author = this.GetAuthorByUsername(postUsernameElement.Value),
                Body = postBodyElement.Value,
                PublishedAtUtc = DateTimeOffset.Parse(postPublishedAtUtcElement.Value),
                Slug = postSlugElement.Value,
                Title = postTitleElement.Value
            };

            var categories = new List<Category>();
            var tags = new List<Tag>();

            var wpCategoriesElements = postElement.Elements("category");
            foreach (var wpCategory in wpCategoriesElements)
            {
                var domainAttribute = wpCategory.Attribute("domain");
                if (domainAttribute == null)
                {
                    throw new XmlException("Unable to parse malformed wordpress categorization.");
                }

                if (domainAttribute.Value == "category")
                {
                    string categorySlug = wpCategory.Attribute("nicename").Value;
                    var category = this.GetCategoryBySlug(categorySlug);
                    categories.Add(category);
                }
                else if (domainAttribute.Value == "post_tag")
                {
                    string tagSlug = wpCategory.Attribute("nicename").Value;
                    var tag = this.GetTagBySlug(tagSlug);
                    tags.Add(tag);
                }
            }

            post.Categories = categories;
            post.Tags = tags;

            return post;
        }

        private Author GetAuthorByUsername(string value)
        {
            return this.Authors.FirstOrDefault(a => a.Username == value);
        }

        private Category GetCategoryBySlug(string categorySlug)
        {
            return this.Categories.FirstOrDefault(c => c.Slug == categorySlug);
        }

        private Tag GetTagBySlug(string tagSlug)
        {
            return this.Tags.FirstOrDefault(t => t.Slug == tagSlug);
        }
    }
}

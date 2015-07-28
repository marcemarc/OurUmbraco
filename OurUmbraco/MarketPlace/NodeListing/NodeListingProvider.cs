﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using OurUmbraco.MarketPlace.Extensions;
using OurUmbraco.MarketPlace.Interfaces;
using OurUmbraco.MarketPlace.Providers;
using OurUmbraco.Wiki.Extensions;
using umbraco;
using umbraco.BusinessLogic;
using Umbraco.Core.Models;
using Umbraco.Web;

namespace OurUmbraco.MarketPlace.NodeListing
{
    public class NodeListingProvider
    {    /// <summary>
         /// get project listing based on ID
         /// </summary>
         /// <param name="id"></param>
         /// <param name="optimized"></param>
         /// <param name="projectKarma"></param>
         /// <returns></returns>
        public IListingItem GetListing(int id, bool optimized = false, int projectKarma = -1)
        {
            var umbracoHelper = new UmbracoHelper(UmbracoContext.Current);
            var content = umbracoHelper.TypedContent(id);

            if (content != null)
                return GetListing(content, optimized, projectKarma);

            throw new NullReferenceException("Content is Null cannot find a node with the id:" + id);
        }

        /// <summary>
        /// get project listing  based on GUID
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="optimized">if set performs less DB interactions to increase speed.</param>
        /// <returns></returns>
        public IListingItem GetListing(Guid guid, bool optimized = false)
        {
            var strGuid = guid.ToString().ToUpper();

            var umbracoHelper = new UmbracoHelper(UmbracoContext.Current);

            // we have to use the translate function to ensure that the casing is the same for comparison as there are GUIDS in the db in both upper and lowercase
            var contents =
                umbracoHelper.TypedContentAtXPath(
                    string.Format(
                        "//Project [@isDoc and translate(packageGuid,'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz') = translate('{0}','ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')]",
                        strGuid));

            var content = contents.FirstOrDefault();

            if (content != null)
            {
                return content.ToIListingItem(optimized);
            }

            throw new NullReferenceException("Node is Null cannot find a node with the guid:" + strGuid);
        }

        /// <summary>
        /// get listing
        /// </summary>
        /// <param name="content"></param>
        /// <param name="optimized">if set performs less DB interactions to increase speed.</param>
        /// <param name="projectKarma"></param>
        /// <returns></returns>
        public IListingItem GetListing(IPublishedContent content, bool optimized = false, int projectKarma = -1)
        {
            if (content != null)
            {
                var listingItem = new ListingItem.ListingItem(
                    p => GetProjectDownloadCount(p),
                    p => projectKarma < 0 ? GetProjectKarma(p) : projectKarma
                    );

                listingItem.Id = content.Id;
                listingItem.NiceUrl = library.NiceUrl(listingItem.Id);
                listingItem.Name = content.Name;
                listingItem.Description = content.GetPropertyValue<string>("description", "");
                listingItem.CurrentVersion = content.GetPropertyValue<string>("version", "");
                listingItem.CurrentReleaseFile = content.GetPropertyValue<string>("file", "");
                listingItem.DefaultScreenshot = content.GetPropertyValue<string>("defaultScreenshotPath", "");
                listingItem.DevelopmentStatus = content.GetPropertyValue<string>("status", "");
                listingItem.ListingType = content.GetPropertyAsListingType("listingType");
                listingItem.GACode = content.GetPropertyValue<string>("gaCode", "");
                listingItem.CategoryId = content.GetPropertyValue<int>("category");
                listingItem.Stable = content.GetPropertyValue<bool>("stable");
                listingItem.Live = content.GetPropertyValue<bool>("projectLive");
                listingItem.LicenseName = content.GetPropertyValue<string>("licenseName", "");
                listingItem.LicenseUrl = content.GetPropertyValue<string>("licenseUrl", "");
                listingItem.ProjectUrl = content.GetPropertyValue<string>("websiteUrl", "");
                listingItem.SupportUrl = content.GetPropertyValue<string>("supportUrl", "");
                listingItem.SourceCodeUrl = content.GetPropertyValue<string>("sourceUrl", "");
                listingItem.DemonstrationUrl = content.GetPropertyValue<string>("demoUrl", "");
                listingItem.OpenForCollab = content.GetPropertyValue<bool>("openForCollab", false);
                listingItem.NotAPackage = content.GetPropertyValue<bool>("notAPackage", false);
                listingItem.ProjectGuid = new Guid(content.GetPropertyValue<string>("packageGuid"));
                listingItem.Approved = content.GetPropertyValue<bool>("approved", false);
                listingItem.UmbracoVerionsSupported = content.GetPropertyValue<string>("compatibleVersions", "").Split(';');
                listingItem.NETVersionsSupported = (content.GetPropertyValue<string>("dotNetVersion", "") != null) ? content.GetPropertyValue<string>("dotNetVersion", "").Split(';') : "".Split(';');
                listingItem.TrustLevelSupported = content.GetPropertyAsTrustLevel("trustLevelSupported");
                listingItem.TermsAgreementDate = content.GetPropertyValue<DateTime>("termsAgreementDate");
                listingItem.CreateDate = content.CreateDate;
                listingItem.VendorId = content.GetPropertyValue<int>("owner");
                listingItem.Logo = content.GetPropertyValue<string>("logo", "");
                listingItem.LicenseKey = content.GetPropertyValue<string>("licenseKey", "");

                //this section was created to speed up loading operations and cut down on the number of database interactions
                if (optimized == false)
                {
                    listingItem.DocumentationFile = GetMediaForProjectByType(content.Id, FileType.docs);
                    listingItem.ScreenShots = GetMediaForProjectByType(content.Id, FileType.screenshot);
                    listingItem.PackageFile = GetMediaForProjectByType(content.Id, FileType.package);
                    listingItem.HotFixes = GetMediaForProjectByType(content.Id, FileType.hotfix);
                    listingItem.SourceFile = GetMediaForProjectByType(content.Id, FileType.source);
                }

                return listingItem;
            }
            throw new NullReferenceException("Content is Null");
        }


        private static int GetProjectDownloadCount(int projectId)
        {
            try
            {
                return Application.SqlHelper.ExecuteScalar<int>(" select count(*) from projectDownload where projectId = @id;",
                    Application.SqlHelper.CreateParameter("@id", projectId));
            }
            catch
            {
                return 0;
            }
        }

        public int GetProjectKarma(int projectId)
        {

            using (var reader = Application.SqlHelper.ExecuteReader("SELECT SUM([points]) AS Karma FROM powersProject WHERE id = @projectId",
                    Application.SqlHelper.CreateParameter("@projectId", projectId)))
                if (reader.Read())
                {
                    return reader.GetInt("Karma");
                }

            return 0;
        }

        public IEnumerable<IMediaFile> GetMediaForProjectByType(int projectId, FileType type)
        {
            var mediaProvider = new MediaProvider();
            return mediaProvider.GetMediaForProjectByType(projectId, type);
        }

        /// <summary>
        /// Persists the listing object to the database
        /// </summary>
        /// <param name="listingItem"></param>
        public void SaveOrUpdate(IListingItem listingItem)
        {
            var contentService = UmbracoContext.Current.Application.Services.ContentService;
            //check if this is a new listing or an existing one.
            var isUpdate = listingItem.Id != 0;
            var content = (isUpdate)
                ? contentService.GetById(listingItem.Id)
                : contentService.CreateContent(listingItem.Name, listingItem.CategoryId, "Project");

            //set all the document properties
            content.SetValue("description", listingItem.Description);
            content.SetValue("version", listingItem.CurrentVersion);
            content.SetValue("file", listingItem.CurrentReleaseFile);
            content.SetValue("status", listingItem.DevelopmentStatus);
            content.SetValue("stable", (listingItem.Stable) ? "1" : "0");
            content.SetValue("projectLive", (listingItem.Live) ? "1" : "0");
            content.SetValue("listingType", listingItem.ListingType.GetListingTypeAsString());
            content.SetValue("gaCode", listingItem.GACode);
            content.SetValue("category", listingItem.CategoryId);
            content.SetValue("licenseName", listingItem.LicenseName);
            content.SetValue("licenseUrl", listingItem.LicenseUrl);
            content.SetValue("supportUrl", listingItem.SupportUrl);
            content.SetValue("sourceUrl", listingItem.SourceCodeUrl);
            content.SetValue("demoUrl", listingItem.DemonstrationUrl);
            content.SetValue("openForCollab", listingItem.OpenForCollab);
            content.SetValue("notAPackage", listingItem.NotAPackage);
            content.SetValue("packageGuid", listingItem.ProjectGuid.ToString());
            content.SetValue("approved", (listingItem.Approved) ? "1" : "0");
            content.SetValue("termsAgreementDate", listingItem.TermsAgreementDate);
            content.SetValue("owner", listingItem.VendorId);
            content.SetValue("websiteUrl", listingItem.ProjectUrl);
            content.SetValue("licenseKey", listingItem.LicenseKey);

            if (listingItem.PackageFile != null)
            {
                var currentFiles = listingItem.PackageFile.Where(x => x.Current && x.Archived == false).ToList();
                var supportedDotNetVersions = new List<string>();
                foreach (
                    var currentFile in currentFiles.Where(x => string.IsNullOrWhiteSpace(x.DotNetVersion) == false && x.DotNetVersion != "nan" && supportedDotNetVersions.Contains(x.DotNetVersion) == false))
                {
                    supportedDotNetVersions.Add(currentFile.DotNetVersion);
                }

                content.SetValue("dotNetVersion", string.Join(",", supportedDotNetVersions));

                var supportedUmbracoVersions = new List<string>();
                foreach (var currentFile in currentFiles.Where(x => string.IsNullOrWhiteSpace(x.UmbVersion.ToVersionString()) == false && x.UmbVersion.ToVersionString() != "nan" && supportedUmbracoVersions.Contains(x.UmbVersion.ToVersionString()) == false))
                {
                    supportedUmbracoVersions.Add(currentFile.UmbVersion.ToVersionString());
                }
                content.SetValue("compatibleVersions", string.Join(",", supportedUmbracoVersions));
            }

            //set the files
            content.SetValue("defaultScreenshotPath", listingItem.DefaultScreenshot);

            if (listingItem.Tags != null && listingItem.Tags.Any())
            {
                var tags = new List<string>();
                foreach (var projectTag in listingItem.Tags.Where(projectTag => tags.Any(x => string.Compare(x, projectTag.Text, StringComparison.InvariantCultureIgnoreCase) == 0)))
                {
                    tags.Add(projectTag.Text);
                }
                content.SetTags("tags", tags, true, "project");
            }


            if (listingItem.DocumentationFile != null)
            {
                if (listingItem.DocumentationFile.Any())
                {
                    content.SetValue("documentation", listingItem.DocumentationFile.OrderBy(x => x.Current).First().Path);
                }
            }

            contentService.SaveAndPublishWithStatus(content);

            listingItem.Id = content.Id;
            listingItem.NiceUrl = library.NiceUrl(listingItem.Id);
        }


        /// <summary>
        /// Gets all listings for a vendor
        /// </summary>
        /// <param name="vendorId"></param>
        /// <param name="optimized">if set performs less DB interactions to increase speed.</param>
        /// <param name="all">If set returns both live and not live listings</param>
        /// <returns></returns>
        public IEnumerable<IListingItem> GetListingsByVendor(int vendorId, bool optimized = false, bool all = false)
        {
            var contents = GetProjectsFromDeliProjectRoot(all).Where(c => c.GetPropertyValue<int>("owner") == vendorId);

            return contents.ToIListingItemList(optimized);
        }

        private static IEnumerable<IPublishedContent> GetProjectsFromDeliProjectRoot(bool all)
        {
            var umbracoHelper = new UmbracoHelper(UmbracoContext.Current);
            var content = umbracoHelper.TypedContent(int.Parse(ConfigurationManager.AppSettings["deliProjectRoot"]));
            if (content == null)
                throw new Exception("Could not find the Deli project root.");
            var contents = content.Descendants().Where(c => c.DocumentTypeAlias == "Project");

            if (all == false)
                contents = contents.Where(p => p.GetPropertyValue<bool>("projectLive"));

            return contents;
        }

        /// <summary>
        /// Returns a listing of projects that a specified member contributes to.
        /// </summary>
        /// <param name="memberId"></param>
        /// <param name="optimized">if set performs less DB interactions to increase speed.</param>
        /// <param name="all">if set returns both live and not live projects.</param>
        /// <returns></returns>
        public IEnumerable<IListingItem> GetListingsForContributor(int memberId, bool optimized = false, bool all = false)
        {

            var umbracoHelper = new UmbracoHelper(UmbracoContext.Current);
            var contribProjects = new List<IPublishedContent>();
            const string sql = @"SELECT * FROM projectContributors WHERE memberId=@memberId";
            var contribPackageIds = UmbracoContext.Current.Application.DatabaseContext.Database.Fetch<int>(sql, new { memberId });

            foreach (var contribPackageId in contribPackageIds)
            {
                contribProjects.Add(umbracoHelper.TypedContent(contribPackageId));
            }

            var listings = new List<IListingItem>();
            foreach (var contribItem in contribProjects)
            {
                listings.Add(GetListing(contribItem.Id, optimized, -1));
            }

            return listings;
        }

        /// <summary>
        /// gets a list of listings
        /// </summary>
        /// <param name="optimized">if set performs less DB interactions to increase speed.</param>
        /// <param name="all">if set returns both live and not live listings</param>
        /// <returns></returns>
        public IEnumerable<IListingItem> GetAllListings(bool optimized = false, bool all = false)
        {
            return GetAllListings(0, 0, optimized, all);
        }
        
        /// <summary>
        /// gets paged list of listings
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <param name="optimized">if set performs less DB interactions to increase speed.</param>
        /// <param name="all">if set returns both live and not live listings</param>
        /// <returns></returns>
        public IEnumerable<IListingItem> GetAllListings(int skip, int take, bool optimized = false, bool all = false)
        {
            var contents = GetProjectsFromDeliProjectRoot(all);

            if (take > 0)
                contents = contents.Skip(skip).Take(take);

            return contents.ToIListingItemList(optimized);
        }

        /// <summary>
        /// Gets a paged list of listings by specified category
        /// </summary>
        /// <param name="category"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <param name="optimized">if set performs less DB interactions to increase speed.</param>
        /// <param name="all">if set returns both live and not live listings</param>
        /// <returns></returns>
        public IEnumerable<IListingItem> GetListingsByCategory(ICategory category, int skip, int take, bool optimized = false, bool all = false)
        {
            var umbracoHelper = new UmbracoHelper(UmbracoContext.Current);
            var categoryContent = umbracoHelper.TypedContent(category.Id);
            var items = categoryContent
                .Children(x => x.DocumentTypeAlias == "Project");
            if (!all)
                items = items.Where(x => x.GetPropertyValue<bool>("projectLive"));
            if (take > 0)
                items = items.Skip(skip).Take(take);
            return items.Select(x => x.ToIListingItem(optimized));
        }
    }
}

using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.Search;
using Sitecore.Search;
using Sitecore.Shell;
using Sitecore.StringExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Sitecore.Support.ContentSearch.Client.Pipelines.Search
{
  public class SearchContentSearchIndex
  {
    public virtual void Process(SearchArgs args)
    {
      Assert.ArgumentNotNull(args, nameof(args));
      if (args.UseLegacySearchEngine) return;
      if (!Settings.GetBoolSetting("BucketConfiguration.ItemBucketsEnabled", true))
      {
        args.UseLegacySearchEngine = true;
      }
      else
      {
        var obj1 = args.Root ?? args.Database.GetRootItem();
        Assert.IsNotNull(obj1, "rootItem");

        if (args.TextQuery.IsNullOrEmpty()) return;

        using (IProviderSearchContext searchContext = ContentSearchManager.GetIndex(new SitecoreIndexableItem(obj1)).CreateSearchContext())
        {
          var source1 = new List<SitecoreUISearchResultItem>();
          try
          {
            Expression<Func<SitecoreUISearchResultItem, bool>> first = PredicateBuilder.False<SitecoreUISearchResultItem>().Or(i => i.Name.StartsWith(args.TextQuery));
            IQueryable<SitecoreUISearchResultItem> source2;
            if (args.ContentLanguage != null && !args.ContentLanguage.Name.IsNullOrEmpty())
            {
              Expression<Func<SitecoreUISearchResultItem, bool>> second = PredicateBuilder.True<SitecoreUISearchResultItem>().And(i => i.Content.Contains(args.TextQuery)).And(i => i.Language.Equals(args.ContentLanguage.Name));
              Expression<Func<SitecoreUISearchResultItem, bool>> predicate = first.Or(second).And(i => i["_latestversion"].Contains("*"));
              source2 = searchContext.GetQueryable<SitecoreUISearchResultItem>().Where(predicate);
            }
            else
            {
              Expression<Func<SitecoreUISearchResultItem, bool>> second = PredicateBuilder.True<SitecoreUISearchResultItem>().And(i => i.Content.Contains(args.TextQuery));
              Expression<Func<SitecoreUISearchResultItem, bool>> predicate = first.Or(second).And(i => i["_latestversion"].Contains("*"));
              source2 = searchContext.GetQueryable<SitecoreUISearchResultItem>().Where(predicate);
            }
            if (args.Root != null && args.Type != SearchType.ContentEditor)
              source2 = source2.Where(i => i.Paths.Contains(args.Root.ID));
            foreach (SitecoreUISearchResultItem searchResultItem1 in source2)
            {
              SitecoreUISearchResultItem result = searchResultItem1;
              if (!UserOptions.View.ShowHiddenItems)
              {
                var obj2 = result.GetItem();
                if (obj2 != null && this.IsHidden(obj2))
                  continue;
              }
              if (args.ContentLanguage != null && !args.ContentLanguage.Name.IsNullOrEmpty())
              {
                SitecoreUISearchResultItem searchResultItem2 = source1.FirstOrDefault(r => r.ItemId == result.ItemId);
                if (searchResultItem2 == null)
                  source1.Add(result);
                else if (searchResultItem2.Language != args.ContentLanguage.Name && result.Language == args.ContentLanguage.Name)
                {
                  source1.Remove(searchResultItem2);
                  source1.Add(result);
                }
              }
              else if (args.Type != SearchType.Classic)
              {
                if (source1.All(r => r.ItemId != result.ItemId))
                  source1.Add(result);
              }
              else
                source1.Add(result);
              if (source1.Count >= args.Limit)
                break;
            }
          }
          catch (Exception ex)
          {
            Log.Error("Invalid lucene search query: " + args.TextQuery, ex, this);
            return;
          }
          foreach (SitecoreUISearchResultItem searchResultItem in source1)
          {
            string title = searchResultItem.DisplayName ?? searchResultItem.Name;
            object obj2 = searchResultItem.Fields.Find(pair => pair.Key == Sitecore.Search.BuiltinFields.Icon).Value ?? Settings.DefaultIcon;
            var empty = string.Empty;
            if (searchResultItem.Uri != null) empty = searchResultItem.Uri.ToString();
            args.Result.AddResult(new SearchResult(title, obj2.ToString(), empty));
          }
        }
      }
    }

    private bool IsHidden(Item item)
    {
      Assert.ArgumentNotNull(item, nameof(item));
      if (item.Appearance.Hidden)
        return true;
      if (item.Parent != null)
        return this.IsHidden(item.Parent);
      return false;
    }
  }
}

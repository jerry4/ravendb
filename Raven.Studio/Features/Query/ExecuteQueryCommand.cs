﻿// -----------------------------------------------------------------------
//  <copyright file="ExecuteQueryCommand.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Client.Connection;
using Raven.Client.Connection.Async;
using Raven.Studio.Features.Documents;
using Raven.Studio.Infrastructure;
using Raven.Studio.Messages;
using Raven.Studio.Models;
using Raven.Client.Extensions;

namespace Raven.Studio.Features.Query
{
	public class ExecuteQueryCommand : Command
	{
		private readonly QueryModel model;
		private readonly IAsyncDatabaseCommands databaseCommands;

		public ExecuteQueryCommand(QueryModel model, IAsyncDatabaseCommands databaseCommands)
		{
			this.model = model;
			this.databaseCommands = databaseCommands;
		}

		public override void Execute(object parameter)
		{
			model.Error = null;
			model.RememberHistory();
			model.DocumentsResult.Value = new DocumentsModel(GetFetchDocumentsMethod);
		}

		private Task GetFetchDocumentsMethod(DocumentsModel documentsModel)
		{
			ApplicationModel.Current.AddNotification(new Notification("Executing query..."));

			var q = new IndexQuery
			{
				Start = (model.Pager.CurrentPage - 1) * model.Pager.PageSize,
				PageSize = model.Pager.PageSize,
				Query = model.Query.Value,
			};

			if (model.SortBy != null && model.SortBy.Count > 0)
			{
				var sortedFields = new List<SortedField>();
				foreach (var sortBy in model.SortBy)
				{
					if (sortBy.EndsWith(QueryModel.SortByDescSuffix))
					{
						var field = sortBy.Remove(sortBy.Length - QueryModel.SortByDescSuffix.Length - 1);
						sortedFields.Add(new SortedField(field) {Descending = true});
					}
					else
						sortedFields.Add(new SortedField(sortBy));
				}
				q.SortedFields = sortedFields.ToArray();
			}

			if (model.IsSpatialQuerySupported)
			{
				q = new SpatialIndexQuery(q)
				    {
				    	Latitude = model.Latitude.HasValue ? model.Latitude.Value : 0,
						Longitude = model.Longitude.HasValue ? model.Longitude.Value : 0,
						Radius = model.Radius.HasValue ? model.Radius.Value : 0,
				    };
			}
			
			return databaseCommands.QueryAsync(model.IndexName, q, null)
				.ContinueWith(task =>
				{
					if (task.Exception != null)
					{
						model.Error = task.Exception.ExtractSingleInnerException().SimplifyError();
						return;
					}
					var qr = task.Result;
					var viewableDocuments = qr.Results.Select(obj => new ViewableDocument(obj.ToJsonDocument())).ToArray();
					documentsModel.Documents.Match(viewableDocuments);
					documentsModel.Pager.TotalResults.Value = qr.TotalResults;
				})
				.ContinueOnSuccess(() => ApplicationModel.Current.AddNotification(new Notification("Query executed.")))
				.Catch();
		}
	}
}
﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Raven.Client.Connection;
using Raven.Client.Document;
using Raven.Client.Extensions;
using Xunit;

namespace Raven.Bundles.Tests.Replication
{
	public class FailoverBetweenTwoMultiTenantDatabases : ReplicationBase
	{
		[Fact]
		public void CanReplicateBetweenTwoMultiTenantDatabases()
		{
			var store1 = CreateStore();
			var store2 = CreateStore();

			store1.DatabaseCommands.EnsureDatabaseExists("FailoverTest");
			store2.DatabaseCommands.EnsureDatabaseExists("FailoverTest");

			SetupReplication(store1.DatabaseCommands.ForDatabase("FailoverTest"),
			                 store2.Url + "/databases/FailoverTest");

			using (var store = new DocumentStore
			                   	{
			                   		DefaultDatabase = "FailoverTest",
			                   		Url = store1.Url,
			                   		Conventions =
			                   			{
			                   				FailoverBehavior = FailoverBehavior.AllowReadsFromSecondariesAndWritesToSecondaries
			                   			}
			                   	})
			{
				store.Initialize();
				var replicationInformerForDatabase = store.GetReplicationInformerForDatabase(null);
				var databaseCommands = (ServerClient) store.DatabaseCommands;
				replicationInformerForDatabase.UpdateReplicationInformationIfNeeded(databaseCommands)
					.Wait();

				var replicationDestinations = replicationInformerForDatabase.ReplicationDestinations;
				
				Assert.NotEmpty(replicationDestinations);

				using (var session = store.OpenSession())
				{
					session.Store(new Item {});
					session.SaveChanges();
				}

				var sanityCheck = store.DatabaseCommands.Head("items/1");
				Assert.NotNull(sanityCheck);

				WaitForDocument(store2.DatabaseCommands.ForDatabase("FailoverTest"), "items/1");
			}
		}

		[Fact]
		public void CanFailoverReplicationBetweenTwoMultiTenantDatabases()
		{
			var store1 = CreateStore();
			var store2 = CreateStore();

			store1.DatabaseCommands.EnsureDatabaseExists("FailoverTest");
			store2.DatabaseCommands.EnsureDatabaseExists("FailoverTest");

			SetupReplication(store1.DatabaseCommands.ForDatabase("FailoverTest"),
			                 store2.Url + "/databases/FailoverTest");

			using (var store = new DocumentStore
			                   	{
			                   		DefaultDatabase = "FailoverTest",
			                   		Url = store1.Url,
			                   		Conventions =
			                   			{
			                   				FailoverBehavior = FailoverBehavior.AllowReadsFromSecondariesAndWritesToSecondaries
			                   			}
			                   	})
			{
				store.Initialize();
				var replicationInformerForDatabase = store.GetReplicationInformerForDatabase(null);
				replicationInformerForDatabase.UpdateReplicationInformationIfNeeded((ServerClient) store.DatabaseCommands)
					.Wait();

				using (var session = store.OpenSession())
				{
					session.Store(new Item {});
					session.SaveChanges();
				}


				WaitForDocument(store2.DatabaseCommands.ForDatabase("FailoverTest"), "items/1");

				servers[0].Dispose();

				using (var session = store.OpenSession())
				{
					var load = session.Load<Item>("items/1");
					Assert.NotNull(load);
				}
			}
		}

		[Fact]
		public void CanFailoverReplicationBetweenTwoMultiTenantDatabases_WithExplicitUrl()
		{
			var store1 = CreateStore();
			var store2 = CreateStore();

			store1.DatabaseCommands.EnsureDatabaseExists("FailoverTest");
			store2.DatabaseCommands.EnsureDatabaseExists("FailoverTest");

			SetupReplication(store1.DatabaseCommands.ForDatabase("FailoverTest"),
			                 store2.Url + "/databases/FailoverTest");

			using (var store = new DocumentStore
								{
									DefaultDatabase = "FailoverTest",
									Url = store1.Url + "/databases/FailoverTest",
									Conventions =
										{
											FailoverBehavior = FailoverBehavior.AllowReadsFromSecondariesAndWritesToSecondaries
										}
								})
			{
				store.Initialize();
				var replicationInformerForDatabase = store.GetReplicationInformerForDatabase("FailoverTest");
				replicationInformerForDatabase.UpdateReplicationInformationIfNeeded((ServerClient) store.DatabaseCommands).Wait();

				Assert.NotEmpty(replicationInformerForDatabase.ReplicationDestinations);

				using (var session = store.OpenSession())
				{
					session.Store(new Item {});
					session.SaveChanges();
				}

				WaitForDocument(store2.DatabaseCommands.ForDatabase("FailoverTest"), "items/1");

				servers[0].Dispose();

				using (var session = store.OpenSession())
				{
					var load = session.Load<Item>("items/1");
					Assert.NotNull(load);
				}
			}
		}

		public class Item
		{
		}
	}
}
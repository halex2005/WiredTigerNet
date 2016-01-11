﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WiredTigerNet;

namespace Tests
{
	[TestFixture]
	public class BasicTest
	{
		private string testDirectory;

		[SetUp]
		public void SetUp()
		{
			testDirectory = Path.GetFullPath(".testData");
			if (Directory.Exists(testDirectory))
				Directory.Delete(testDirectory, true);
			Directory.CreateDirectory(testDirectory);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(testDirectory))
				Directory.Delete(testDirectory, true);
		}

		[Test]
		public void Simple()
		{
			using (var connection = Connection.Open(testDirectory, "create", null))
			using (var session = connection.OpenSession())
			{
				session.Create("table:test",
					"key_format=u,value_format=u,prefix_compression=true,block_compressor=snappy,columns=(key,scopeKey)");
				session.Create("index:test:byScopeKey", "prefix_compression=true,block_compressor=snappy,columns=(scopeKey)");

				using (var cursor = session.OpenCursor("table:test"))
				{
					cursor.Insert("a", "k");
					cursor.Insert("b", "k");
				}

				using (var cursor = session.OpenCursor("index:test:byScopeKey(key)"))
					cursor.AssertKeyValues("k", "a", "b");
			}
		}

		[Test]
		public void SimpleWithTran()
		{
			using (var connection = Connection.Open(testDirectory, "create", null))
			using (var session = connection.OpenSession())
			{
				session.Create("table:test",
					"key_format=u,value_format=u,prefix_compression=true,block_compressor=snappy,columns=(key,scopeKey)");
				session.Create("index:test:byScopeKey", "prefix_compression=true,block_compressor=snappy,columns=(scopeKey)");

				using (var cursor = session.OpenCursor("table:test"))
				{
					cursor.Insert("a", "k");
					cursor.Insert("b", "k");
				}
				session.BeginTran();
				using (var cursor = session.OpenCursor("table:test"))
					cursor.Insert("c", "k");
				using (var cursor = session.OpenCursor("index:test:byScopeKey(key)"))
					cursor.AssertKeyValues("k", "a", "b", "c");
				session.RollbackTran();
				using (var cursor = session.OpenCursor("index:test:byScopeKey(key)"))
					cursor.AssertKeyValues("k", "a", "b");
			}
		}

		[Test]
		public void SessionCreateConfigParameterIsNullable()
		{
			using (var connection = Connection.Open(testDirectory, "create", null))
			using (var session = connection.OpenSession())
			{
				session.Create("table:test", null);
				using (var cursor = session.OpenCursor("table:test"))
				{
					cursor.Insert("a", "k");
					cursor.Insert("b", "k");
				}
				using (var cursor = session.OpenCursor("table:test"))
					cursor.AssertAllKeysAndValues("a->k", "b->k");
			}
		}

		[Test]
		public void ConnectionOpenConfigParameterIsNullable()
		{
			using (Connection.Open(testDirectory, "create", null))
			{
			}

			using (var connection = Connection.Open(testDirectory, null, null))
			using (var session = connection.OpenSession())
			{
				session.Create("table:test", null);
				using (var cursor = session.OpenCursor("table:test"))
					cursor.Insert("a", "b");
				using (var cursor = session.OpenCursor("table:test"))
					cursor.AssertAllKeysAndValues("a->b");
			}
		}

		[Test]
		public void CorrectlyLogErrorWhenTargetDirectoryNotExist()
		{
			var eventHandler = new LoggingEventHandler();
			var exception = Assert.Throws<WiredTigerApiException>(() => Connection.Open(Path.Combine(testDirectory, "inexistentFolder"),
				"", eventHandler));
			const int expectedErrorCode = -28997;
			Assert.That(exception.Message, Is.StringContaining("The system cannot find the path specified")
				.Or.StringContaining("найти указанный файл"));
			Assert.That(exception.Message, Is.StringContaining(expectedErrorCode.ToString(CultureInfo.InvariantCulture)));
			Assert.That(exception.ApiName, Is.EqualTo("wiredtiger_open"));
			Assert.That(exception.ErrorCode, Is.EqualTo(expectedErrorCode));
			Assert.That(eventHandler.loggedEvents.Count, Is.EqualTo(1));
			var loggedEvent = (LoggingEventHandler.ErrorEvent) eventHandler.loggedEvents.Single();
			Assert.That(loggedEvent.errorCode, Is.EqualTo(expectedErrorCode));
			Assert.That(loggedEvent.errorString, Is.StringContaining("The system cannot find the path specified")
				.Or.StringContaining("найти указанный файл"));
			Assert.That(loggedEvent.message, Is.StringContaining(testDirectory));
		}

		[Test]
		public void HandleCrashesOfErrorHandler()
		{
			var eventHandler = new CrashingEventHandler();
			var exception = Assert.Throws<WiredTigerApiException>(() => Connection.Open(Path.Combine(testDirectory, "inexistentFolder"),
				"", eventHandler));
			const int expectedErrorCode = -28997;
			Assert.That(exception.Message, Is.StringContaining("The system cannot find the path specified")
				.Or.StringContaining("найти указанный файл"));
			Assert.That(exception.Message, Is.StringContaining(expectedErrorCode.ToString(CultureInfo.InvariantCulture)));
		}
	}
}
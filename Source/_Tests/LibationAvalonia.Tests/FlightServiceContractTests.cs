using DataLayer;
using LibationAvalonia.Features.Flight;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class FlightServiceContractTests
{
	[TestMethod]
	public async Task SelectionUndoReconcileAndPersistence_PreserveStableIdentity()
	{
		await HeadlessTestHost.Reset();
		await HeadlessTestHost.Dispatch(() =>
		{
			HeadlessTestHost.Configuration.PersistFlightBetweenSessions = true;
			var alpha = CreateBook("A000000001", "Alpha", 120);
			var beta = CreateBook("B000000002", "Beta", 180);
			var gamma = CreateBook("C000000003", "Gamma", 240);

			using (var flight = new FlightService(HeadlessTestHost.Configuration))
			{
				Assert.AreEqual(3, flight.AddRange([alpha, beta, gamma]));
				Assert.IsFalse(flight.Add(alpha));
				flight.SetVisibleItems([alpha, gamma]);
				Assert.AreEqual(1, flight.HiddenCount);

				var undo = flight.Remove(new("B000000002"));
				Assert.IsTrue(undo.CanRestore);
				Assert.IsTrue(flight.Restore(undo));
				CollectionAssert.AreEqual(
					new[] { "A000000001", "B000000002", "C000000003" },
					HeadlessTestHost.Configuration.ContemporaryFlightProductIds);
			}

			using var restored = new FlightService(HeadlessTestHost.Configuration);
			restored.ReconcileLibrary([gamma, CreateBook("B000000002", "Beta refreshed", 181), alpha]);
			Assert.AreEqual(3, restored.Count);
			CollectionAssert.AreEqual(
				new[] { "A000000001", "B000000002", "C000000003" },
				restored.Items.Select(item => item.Id.ProductId).ToArray());
			Assert.AreEqual("Beta refreshed", restored.Items[1].LibraryBook.Book.Title);

			restored.ReconcileLibrary([alpha, gamma]);
			Assert.AreEqual(2, restored.Count);
			CollectionAssert.AreEqual(
				new[] { "A000000001", "C000000003" },
				HeadlessTestHost.Configuration.ContemporaryFlightProductIds);
		});
	}

	private static LibraryBook CreateBook(string productId, string title, int minutes)
	{
		var book = new Book(
			new AudibleProductId(productId),
			title,
			string.Empty,
			"Test fixture",
			minutes,
			ContentType.Product,
			[new Contributor("Author", "AUTHOR0001")],
			[new Contributor("Narrator", "NARRATOR01")],
			"us");
		return new(book, new DateTime(2026, 8, 31), "test-account");
	}
}

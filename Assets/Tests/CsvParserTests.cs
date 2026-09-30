using System.Collections.Generic;
using NUnit.Framework;
using SBGames.Database.CSV;

namespace SBGames.Tests
{
    [TestFixture]
    public class CsvParserTests
    {
        [Test]
        public void ParseRows_SimpleCsv_ReturnsExpectedRowsAndColumns()
        {
            string csv = "Id,Name,Cost\nitem_01,Iron Sword,100\nitem_02,Shield,50";
            var rows = CsvParser.ParseRows(csv);

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("Id", rows[0][0]);
            Assert.AreEqual("item_01", rows[1][0]);
            Assert.AreEqual("Iron Sword", rows[1][1]);
            Assert.AreEqual("100", rows[1][2]);
            Assert.AreEqual("item_02", rows[2][0]);
        }

        [Test]
        public void ParseTable_WithHeaders_ReturnsMappedDictionaries()
        {
            string csv = "Id,Name,Damage\nsword_fire,Flame Blade,25.5\nstaff_frost,Ice Staff,18.0";
            var table = CsvParser.ParseTable(csv);

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("sword_fire", table[0]["Id"]);
            Assert.AreEqual("Flame Blade", table[0]["Name"]);
            Assert.AreEqual("25.5", table[0]["Damage"]);

            Assert.AreEqual("staff_frost", table[1]["Id"]);
            Assert.AreEqual("Ice Staff", table[1]["Name"]);
            Assert.AreEqual("18.0", table[1]["Damage"]);
        }

        [Test]
        public void ParseTable_WithQuotedCommasAndEscapedQuotes_HandlesCorrectly()
        {
            string csv = "Id,Description\nitem_scroll,\"Ancient scroll, deals \"\"massive\"\" damage.\"\nitem_potion,Simple potion";
            var table = CsvParser.ParseTable(csv);

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("Ancient scroll, deals \"massive\" damage.", table[0]["Description"]);
            Assert.AreEqual("Simple potion", table[1]["Description"]);
        }

        [Test]
        public void SerializeTable_RoundtripsSuccessfully()
        {
            var headers = new[] { "Id", "Description", "Value" };
            var rows = new List<Dictionary<string, string>>
            {
                new() { ["Id"] = "item_1", ["Description"] = "Healing potion, recovers 50 HP", ["Value"] = "25" },
                new() { ["Id"] = "item_2", ["Description"] = "Rune of Fire", ["Value"] = "100" }
            };

            string csv = CsvParser.SerializeTable(headers, rows);
            var parsed = CsvParser.ParseTable(csv);

            Assert.AreEqual(2, parsed.Count);
            Assert.AreEqual("item_1", parsed[0]["Id"]);
            Assert.AreEqual("Healing potion, recovers 50 HP", parsed[0]["Description"]);
            Assert.AreEqual("25", parsed[0]["Value"]);
            Assert.AreEqual("item_2", parsed[1]["Id"]);
        }
    }
}

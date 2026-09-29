using System.Linq;
using hamlex.Views.Prints;
using Xunit;

namespace hamlex.Views.Prints.Tests
{
    public class InvoicePagePlannerTests
    {
        [Fact]
        public void ShortInvoiceStaysOnOnePage()
        {
            var pages = InvoicePagePlanner.Plan(12, 32);

            Assert.Single(pages);
            Assert.Equal(0, pages[0].Start);
            Assert.Equal(12, pages[0].Count);
        }

        [Fact]
        public void ExactPageStaysOnOnePage()
        {
            var pages = InvoicePagePlanner.Plan(32, 32);

            Assert.Single(pages);
            Assert.Equal(32, pages[0].Count);
        }

        [Fact]
        public void ThirtyNineRowsAtThirtyTwoPerPagePrintTwoSheets()
        {
            int perPage = InvoicePagePlanner.RowsInSlot(32 * 22, 22);

            Assert.Equal(32, perPage);
            var pages = InvoicePagePlanner.Plan(39, perPage);

            Assert.Equal(new[] { 32, 7 }, pages.Select(p => p.Count).ToArray());
        }

        [Fact]
        public void SlotShorterThanARowHasNoCapacity()
        {
            Assert.Equal(0, InvoicePagePlanner.RowsInSlot(10, 22));
            Assert.Equal(0, InvoicePagePlanner.RowsInSlot(0, 22));
        }

        [Fact]
        public void ThirtyThirdRowOpensASecondPage()
        {
            var pages = InvoicePagePlanner.Plan(33, 32);

            Assert.Equal(2, pages.Count);
            Assert.Equal(32, pages[0].Count);
            Assert.Equal(32, pages[1].Start);
            Assert.Equal(1, pages[1].Count);
        }

        [Fact]
        public void RowsAreCoveredOnceAcrossThreePages()
        {
            var pages = InvoicePagePlanner.Plan(70, 30);

            Assert.Equal(new[] { 30, 30, 10 }, pages.Select(p => p.Count).ToArray());
            Assert.Equal(Enumerable.Range(0, 70), pages.SelectMany(Cover));
        }

        [Fact]
        public void EmptyInvoiceStillPrintsTheHeaderPage()
        {
            var pages = InvoicePagePlanner.Plan(0, 30);

            Assert.Single(pages);
            Assert.Equal(0, pages[0].Count);
        }

        [Fact]
        public void InvalidCapacityPrintsOneRowPerPage()
        {
            var pages = InvoicePagePlanner.Plan(3, 0);

            Assert.Equal(3, pages.Count);
            Assert.All(pages, page => Assert.Equal(1, page.Count));
        }

        [Fact]
        public void RowHangingPastTheHostDoesNotFit()
        {
            bool fits = InvoicePagePlanner.ContentFits(
                rootDesiredHeight: 1123,
                pageHeight: 1123,
                hostDesiredHeight: 704,
                hostActualHeight: 704,
                hostBottom: 900,
                legalBottom: 1100,
                lastRowBottom: 980);

            Assert.False(fits);
        }

        [Fact]
        public void ClippedRowHostDoesNotFitOnThePage()
        {
            // The current one-page layout: the row slot stays put and the extra rows overflow it.
            bool fits = InvoicePagePlanner.ContentFits(
                rootDesiredHeight: 1123,
                pageHeight: 1123,
                hostDesiredHeight: 880,
                hostActualHeight: 660,
                hostBottom: 900,
                legalBottom: 1100);

            Assert.False(fits);
        }

        [Fact]
        public void RowsThatStayInsideTheSlotFit()
        {
            bool fits = InvoicePagePlanner.ContentFits(
                rootDesiredHeight: 1123,
                pageHeight: 1123,
                hostDesiredHeight: 440,
                hostActualHeight: 660,
                hostBottom: 700,
                legalBottom: 1100);

            Assert.True(fits);
        }

        [Fact]
        public void LegalTextPushedPastTheSheetDoesNotFit()
        {
            bool fits = InvoicePagePlanner.ContentFits(
                rootDesiredHeight: 1123,
                pageHeight: 1123,
                hostDesiredHeight: 700,
                hostActualHeight: 700,
                hostBottom: 950,
                legalBottom: 1300);

            Assert.False(fits);
        }

        [Fact]
        public void GrowingRootPastA4DoesNotFit()
        {
            bool fits = InvoicePagePlanner.ContentFits(
                rootDesiredHeight: 1500,
                pageHeight: 1123,
                hostDesiredHeight: 900,
                hostActualHeight: 900,
                hostBottom: 1100,
                legalBottom: 1400);

            Assert.False(fits);
        }

        private static System.Collections.Generic.IEnumerable<int> Cover(InvoicePagePlanner.Slice slice)
        {
            return Enumerable.Range(slice.Start, slice.Count);
        }
    }
}

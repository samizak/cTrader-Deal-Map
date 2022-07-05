using cAlgo.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class DealMap : Indicator
    {
        private readonly string TAG = Guid.NewGuid().ToString() + "-DEALMAP";

        public enum DisplayType
        {
            Both,
            Percentage,
            Net,
            None
        }

        #region Parameters
        [Parameter(name: "Display Type", DefaultValue = DisplayType.Both)]
        public DisplayType DisplayType_ { get; set; }

        [Parameter(name: "Opacity (%)", Group = "Deal Map", DefaultValue = 100, MinValue = 0, MaxValue = 100)]
        public int Opacity { get; set; }

        [Parameter(name: "Thickness", Group = "Deal Map", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int Thickness { get; set; }

        [Parameter(name: "Show Profit Deals?", Group = "Deal Map", DefaultValue = true)]
        public bool ShowProfitDeals { get; set; }

        [Parameter(name: "Show Loss Deals?", Group = "Deal Map", DefaultValue = true)]
        public bool ShowLossDeals { get; set; }

        [Parameter(name: "Bearish", Group = "Colours", DefaultValue = "Red")]
        public string BearishColour { get; set; }

        [Parameter(name: "Bullish", Group = "Colours", DefaultValue = "Cyan")]
        public string BullishColour { get; set; }

        #endregion Parameters


        #region Helper Functions
        /// <summary>
        /// Get all opened positions for current Symbol
        /// </summary>
        public Position[] GetSymbolOpenPositions()
        {
            return Positions
                .Where(position => position.SymbolName == Symbol.Name)
                .ToArray();
        }

        /// <summary>
        /// Calculate Net Profit of all Opened Positions
        /// </summary>
        public double CalculateOpenedPositionsNetProfit()
        {
            return GetSymbolOpenPositions()
                .Sum(openedPosition => openedPosition.NetProfit);
        }

        // Remap Transparency Scale from 0-255 to 0-100 and back
        private static int MapValue(int opacity)
            => opacity * 255 / 100;

        // Get the Colour from Net Profit
        private Color GetColour(double netProfit)
            => netProfit > 0 ? BullishColour.ToString() : netProfit < 0 ? BearishColour.ToString() : Color.White;

        #endregion


        public override void Calculate(int index) { }

        protected override void Initialize()
        {
            CallEvents();
            Timer.Start(1);
        }  
        #region Display Text


        private void DisplayText()
        {
            double netProfit = CalculateOpenedPositionsNetProfit();
            string textVal = GetText(netProfit);
            Color textCol = GetColour(netProfit);

            string textName = TAG + "text";
            Chart.DrawText(textName, textVal, Bars.ClosePrices.Count - 1 + 3, Bars.ClosePrices.LastValue, textCol);
        }

        private void DrawDeal(Position[] positions, DateTime currentTime)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                if (positions[i].SymbolName != SymbolName)
                    continue;

                bool showProfitDeals = ShowProfitDeals && positions[i].NetProfit > 0;
                bool showLosingDeals = ShowLossDeals && positions[i].NetProfit < 0;

                if (!(showLosingDeals || showProfitDeals))
                    continue;

                // Get DateTime of Entry and Exit of Trade
                DateTime startDate = positions[i].EntryTime;
                DateTime endDate = currentTime;

                // Convert DateTime to Index for better accuracy
                int startIndex = Bars.OpenTimes.GetIndexByTime(startDate);
                int endIndex = Bars.OpenTimes.GetIndexByTime(endDate);

                // Get Entry and Closing price of Trade
                double startPrice = positions[i].EntryPrice;
                double closePrice = Bars.ClosePrices.LastValue;

                string dealMapName = TAG + "Deal" + i;

                GetDealColour(positions[i], out Color dealCol);
                Chart.DrawTrendLine(dealMapName, startIndex, startPrice, endIndex, closePrice, color: dealCol, thickness: Thickness);
            }
        }

        private void DrawDealMaps()
        {
            if (GetSymbolOpenPositions().Length == 0)
                return;

            Position[] positions = Positions.ToArray();
            DateTime now = DateTime.Now;
            DateTime currentTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

            DrawDeal(positions, currentTime);
            DisplayText();
        }

        private void GetDealColour(Position position, out Color dealCol)
            => dealCol = Color.FromArgb(MapValue(Opacity), GetColour(position.NetProfit));

        private string GetText(double netProfit)
        {
            string sign = netProfit > 0 ? "+" : "";
            string getNetProfit = $"{sign}{netProfit:C}";
            string getPercentage = $"{sign}{netProfit / Account.Balance:0.00%}";

            switch (DisplayType_)
            {
                case DisplayType.Both:
                    return $"{getNetProfit}\n{getPercentage}";

                case DisplayType.Net:
                    return getNetProfit;

                case DisplayType.Percentage:
                    return getPercentage;

                case DisplayType.None:
                default:
                    return "";
            }
        }
        #endregion Display Text

        #region Events
        /// <summary>
        /// Update Deal Maps on all Events
        /// </summary>
        public void CallEvents()
        {
            Positions.Opened += OnPositionsOpened;
            Positions.Closed += OnPositionsClosed;
            Positions.Modified += OnPositionsModified;
            Chart.ScrollChanged += OnChartScrollChanged;
            Chart.SizeChanged += OnChartSizeChanged;
            Chart.ZoomChanged += OnChartZoomChanged;
        }

        public void RemoveObjects()
        {
            var cobjects = Chart.Objects.Where(o => o.Name.StartsWith(TAG)).ToList();
            foreach (var cobj in cobjects)
                Chart.RemoveObject(cobj.Name);
        }

        protected override void OnTimer()
            => DrawDealMaps();

        private void OnChartScrollChanged(ChartScrollEventArgs _)
            => RemoveObjects();

        private void OnChartSizeChanged(ChartSizeEventArgs _)
            => RemoveObjects();

        private void OnChartZoomChanged(ChartZoomEventArgs _)
            => RemoveObjects();

        private void OnPositionsClosed(PositionClosedEventArgs _)
            => RemoveObjects();

        private void OnPositionsModified(PositionModifiedEventArgs _)
            => RemoveObjects();

        private void OnPositionsOpened(PositionOpenedEventArgs _)
            => RemoveObjects();
        #endregion Events
    }
}

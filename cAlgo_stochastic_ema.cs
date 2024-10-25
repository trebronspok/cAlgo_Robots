using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class StochasticCrossover : Robot
    {
        [Parameter("K Period", DefaultValue = 8)]
        public int KPeriod { get; set; }

        [Parameter("D Period", DefaultValue = 3)]
        public int DPeriod { get; set; }

        [Parameter("Slowing", DefaultValue = 3)]
        public int Slowing { get; set; }

        [Parameter("ATR Period", DefaultValue = 14)]
        public int ATRPeriod { get; set; }

        [Parameter("Take Profit Multiplier", DefaultValue = 3.0)]
        public double TakeProfitMultiplier { get; set; }

        [Parameter("Risk Percentage", DefaultValue = 1.0)]
        public double RiskPercentage { get; set; }

        [Parameter("EMA Period", DefaultValue = 50)]
        public int EMAPeriod { get; set; }

        [Parameter("EMA Timeframe", DefaultValue = "Hour")]
        public TimeFrame EMA_TimeFrame { get; set; }

        private StochasticOscillator _stochastic;
        private AverageTrueRange _atr;
        private ExponentialMovingAverage _ema;

        protected override void OnStart()
        {
            _stochastic = Indicators.StochasticOscillator(KPeriod, DPeriod, Slowing, MovingAverageType.Simple);
            _atr = Indicators.AverageTrueRange(ATRPeriod, MovingAverageType.Simple);
            _ema = Indicators.ExponentialMovingAverage(MarketData.GetSeries(EMA_TimeFrame).Close, EMAPeriod);
        }

        protected override void OnBar()
        {
            // Check if there are any open positions
            if (Positions.FindAll("StochasticCrossover", SymbolName).Length > 0)
                return;
                
            var kValue = _stochastic.PercentK.Last(1);
            var dValue = _stochastic.PercentD.Last(1);
            var atrValue = _atr.Result.Last(1);
            var emaValue = _ema.Result.LastValue;

            double riskAmount = Account.Balance * (RiskPercentage / 100);
            double stopLossPips = atrValue / Symbol.PipSize; // Initial stop loss based on ATR
            double volume = riskAmount / (stopLossPips * Symbol.PipValue);

            volume = Symbol.NormalizeVolume(volume, RoundingMode.Down);

            // Recalculate stop loss based on the computed volume
            stopLossPips = riskAmount / (volume * Symbol.PipValue);

            double takeProfitPips = atrValue * TakeProfitMultiplier / Symbol.PipSize;

            if (kValue < 30 && dValue < 30 && kValue > dValue && MarketSeries.Close.LastValue > emaValue)
            {
                ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, "StochasticCrossover", stopLossPips, takeProfitPips);
            }
            else if (kValue > 70 && dValue > 70 && kValue < dValue && MarketSeries.Close.LastValue < emaValue)
            {
                ExecuteMarketOrder(TradeType.Sell, SymbolName, volume, "StochasticCrossover", stopLossPips, takeProfitPips);
            }

            // Display the current account balance on the chart
            Chart.DrawText("AccountBalance", $"Balance: {Account.Balance}", StaticPosition.TopLeft, Colors.White);
        }
    }
}

using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System.Linq;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class EnhancedMovingAverageCrossoverWithATR : Robot
    {
        private MovingAverage _fastMa;
        private MovingAverage _slowMa;
        private RelativeStrengthIndex _rsi;
        private AverageTrueRange _atr;

        [Parameter("Fast MA Period", DefaultValue = 10)]
        public int FastMaPeriod { get; set; }

        [Parameter("Slow MA Period", DefaultValue = 50)]
        public int SlowMaPeriod { get; set; }

        [Parameter("RSI Period", DefaultValue = 14)]
        public int RsiPeriod { get; set; }

        [Parameter("RSI Overbought Level", DefaultValue = 70)]
        public int RsiOverbought { get; set; }

        [Parameter("RSI Oversold Level", DefaultValue = 30)]
        public int RsiOversold { get; set; }

        [Parameter("RSI Lookback Period", DefaultValue = 5)]
        public int RsiLookbackPeriod { get; set; }

        [Parameter("ATR Period", DefaultValue = 14)]
        public int AtrPeriod { get; set; }

        [Parameter("ATR Stop Loss Multiplier", DefaultValue = 2.0)]
        public double AtrStopLossMultiplier { get; set; }

        [Parameter("ATR Take Profit Multiplier", DefaultValue = 2.0)]
        public double AtrTakeProfitMultiplier { get; set; }

        [Parameter("Risk Percentage", DefaultValue = 1.0)]
        public double RiskPercentage { get; set; }

        [Parameter("Close on Reversal Signal", DefaultValue = true)]
        public bool CloseOnReversalSignal { get; set; }

        protected override void OnStart()
        {
            _fastMa = Indicators.MovingAverage(MarketSeries.Close, FastMaPeriod, MovingAverageType.Simple);
            _slowMa = Indicators.MovingAverage(MarketSeries.Close, SlowMaPeriod, MovingAverageType.Simple);
            _rsi = Indicators.RelativeStrengthIndex(MarketSeries.Close, RsiPeriod);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);
        }

        protected override void OnBarClosed()
        {
            try
            {
                bool isRsiBelowThreshold = true;
                bool isRsiAboveThreshold = true;

                for (int i = 0; i < RsiLookbackPeriod; i++)
                {
                    if (_rsi.Result.Last(i) >= RsiOversold)
                    {
                        isRsiBelowThreshold = false;
                    }
                    if (_rsi.Result.Last(i) <= RsiOverbought)
                    {
                        isRsiAboveThreshold = false;
                    }
                }

                double atrValue = _atr.Result.LastValue;
                double stopLossPrice;
                double takeProfitPrice;

                double accountBalance = Account.Balance;
                double riskAmount = accountBalance * (RiskPercentage / 100);
                double lotSize;

                if (_fastMa.Result.LastValue > _slowMa.Result.LastValue && isRsiBelowThreshold)
                {
                    if (CloseOnReversalSignal)
                    {
                        ClosePositions(TradeType.Sell);
                    }

                    if (!HasOpenPosition(TradeType.Buy))
                    {
                        stopLossPrice = NormalizePrice(Symbol.Bid - (atrValue * AtrStopLossMultiplier));
                        takeProfitPrice = NormalizePrice(Symbol.Bid + (atrValue * AtrTakeProfitMultiplier));
                        lotSize = riskAmount / ((Symbol.Bid - stopLossPrice) * Symbol.PipValue);
                        ExecuteMarketOrder(TradeType.Buy, SymbolName, lotSize, "Buy", stopLossPrice, takeProfitPrice);
                    }
                }
                else if (_fastMa.Result.LastValue < _slowMa.Result.LastValue && isRsiAboveThreshold)
                {
                    if (CloseOnReversalSignal)
                    {
                        ClosePositions(TradeType.Buy);
                    }

                    if (!HasOpenPosition(TradeType.Sell))
                    {
                        stopLossPrice = NormalizePrice(Symbol.Ask + (atrValue * AtrStopLossMultiplier));
                        takeProfitPrice = NormalizePrice(Symbol.Ask - (atrValue * AtrTakeProfitMultiplier));
                        lotSize = riskAmount / ((stopLossPrice - Symbol.Ask) * Symbol.PipValue);
                        ExecuteMarketOrder(TradeType.Sell, SymbolName, lotSize, "Sell", stopLossPrice, takeProfitPrice);
                    }
                }
            }
            catch (Exception ex)
            {
                Print("Error: ", ex.Message);
            }
        }

        private double NormalizePrice(double price)
        {
            return Math.Round(price, Symbol.Digits);
        }

        private bool HasOpenPosition(TradeType tradeType)
        {
            return Positions.Any(p => p.SymbolCode == Symbol.Code && p.TradeType == tradeType);
        }

        private void ClosePositions(TradeType tradeType)
        {
            foreach (var position in Positions.Where(p => p.SymbolCode == Symbol.Code && p.TradeType == tradeType))
            {
                ClosePosition(position);
            }
        }
    }
}

using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System.Linq;
using System.Net;
using System.Net.Mail;

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

        [Parameter("Trading Enabled", DefaultValue = true)]
        public bool TradingEnabled { get; set; }

        [Parameter("Email From", DefaultValue = "your-email@example.com")]
        public string EmailFrom { get; set; }

        [Parameter("Email To", DefaultValue = "recipient-email@example.com")]
        public string EmailTo { get; set; }

        [Parameter("SMTP Server", DefaultValue = "smtp.example.com")]
        public string SmtpServer { get; set; }

        [Parameter("SMTP Port", DefaultValue = 587)]
        public int SmtpPort { get; set; }

        [Parameter("SMTP Username", DefaultValue = "your-email@example.com")]
        public string SmtpUsername { get; set; }

        [Parameter("SMTP Password", DefaultValue = "your-email-password", IsPassword = true)]
        public string SmtpPassword { get; set; }

        protected override void OnStart()
        {
            _fastMa = Indicators.MovingAverage(MarketSeries.Close, FastMaPeriod, MovingAverageType.Simple);
            _slowMa = Indicators.MovingAverage(MarketSeries.Close, FastMaPeriod, MovingAverageType.Simple);
            _rsi = Indicators.RelativeStrengthIndex(MarketSeries.Close, RsiPeriod);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);

            Positions.Closed += OnPositionClosed;
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

                if (_fastMa.Result.LastValue > _slowMa.Result.LastValue && _fastMa.Result.Last(1) <= _slowMa.Result.Last(1) && isRsiBelowThreshold)
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

                        if (TradingEnabled)
                        {
                            ExecuteMarketOrder(TradeType.Buy, SymbolName, lotSize, "Buy", stopLossPrice, takeProfitPrice);
                        }
                        else
                        {
                            Chart.DrawIcon("BuySignal" + MarketSeries.Close.Count, ChartIconType.UpArrow, MarketSeries.OpenTime.LastValue, Symbol.Bid, Color.Green);
                        }
                    }
                }
                else if (_fastMa.Result.LastValue < _slowMa.Result.LastValue && _fastMa.Result.Last(1) >= _slowMa.Result.Last(1) && isRsiAboveThreshold)
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

                        if (TradingEnabled)
                        {
                            ExecuteMarketOrder(TradeType.Sell, SymbolName, lotSize, "Sell", stopLossPrice, takeProfitPrice);
                        }
                        else
                        {
                            Chart.DrawIcon("SellSignal" + MarketSeries.Close.Count, ChartIconType.DownArrow, MarketSeries.OpenTime.LastValue, Symbol.Ask, Color.Red);
                        }
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

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;
            SendEmailNotification(position);
        }

        private void SendEmailNotification(Position position)
        {
            try
            {
                double profitLoss = position.GrossProfit;
                string subject = $"Position Closed: {position.TradeType}";
                string body = $"Position {position.TradeType} closed.\nSymbol: {position.SymbolCode}\nVolume: {position.Volume}\nProfit/Loss: {profitLoss}\nClosed by: {position.CloseReason}";

                MailMessage mail = new MailMessage(EmailFrom, EmailTo, subject, body);
                SmtpClient client = new SmtpClient(SmtpServer, SmtpPort)
                {
                    Credentials = new NetworkCredential(SmtpUsername, SmtpPassword),
                    EnableSsl = true
                };

                client.Send(mail);
                Print("Email sent successfully.");
            }
            catch (Exception ex)
            {
                Print("Failed to send email: ", ex.Message);
            }
        }
    }
}

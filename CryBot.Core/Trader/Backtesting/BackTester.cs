﻿using Bittrex.Net.Objects;

using CryBot.Core.Exchange;
using CryBot.Core.Strategies;
using CryBot.Core.Exchange.Models;

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CryBot.Core.Trader.Backtesting
{
    public class BackTester
    {
        private readonly ICryptoApi _cryptoApi;
        private static volatile object _syncObject = new object();

        public BackTester(ICryptoApi cryptoApi)
        {
            _cryptoApi = cryptoApi;
        }

        public async Task<BacktestingStats> FindBestSettings(string market)
        {
            var candlesResponse = await _cryptoApi.GetCandlesAsync(market, TickInterval.OneMinute);
            var candles = candlesResponse.Content;
            var buyLowerRange = new List<decimal> { -5, -4, -3, -2, -1 };
            var minimumTakeProfitRange = new List<decimal> {  0, 0.1M, 0.5M, 3, 5 };
            var highStopLossRange = new List<decimal> { -30,  -5, -4, -3, -2, -1, -0.1M };
            var stopLossRange = new List<decimal> { -30, -20, -10, -5, -4, -3 };
            var buyTriggerRange = new List<decimal> { -7, -6, -5, -4, -3, -2 };
            var bestSettings = TraderSettings.Default;
            var bestProfit = -100M;
            var totalIterations = buyLowerRange.Count * highStopLossRange.Count * stopLossRange.Count *
                                  buyTriggerRange.Count * minimumTakeProfitRange.Count;

            var it = 0;
            var strategies = new List<HoldUntilPriceDropsStrategy>();
            foreach (var buy in buyLowerRange)
            {
                foreach (var highStopLoss in highStopLossRange)
                {
                    foreach (var stopLoss in stopLossRange)
                    {
                        foreach (var trigger in buyTriggerRange)
                        {
                            foreach (var minProfit in minimumTakeProfitRange)
                            {
                                var strategy = new HoldUntilPriceDropsStrategy();
                                strategy.Settings = new TraderSettings
                                {
                                    BuyLowerPercentage = buy,
                                    HighStopLossPercentage = highStopLoss,
                                    StopLoss = stopLoss,
                                    BuyTrigger = trigger,
                                    MinimumTakeProfit = minProfit,
                                    TradingBudget = TraderSettings.Default.TradingBudget
                                };
                                strategies.Add(strategy);
                            }
                        }
                    }
                }
            }
            var dict = new Dictionary<string, CryptoTraderStats>();
            var oldPercentage = -1;
            Parallel.ForEach(strategies, (strategy) =>
            {
                try
                {
                    var backtester = new CryptoTraderBacktester();
                    backtester.Strategy = strategy;
                    backtester.Candles = candles;
                    backtester.Initialize();
                    var cryptoTraderStats = backtester.StartFromFile(market);
                    it++;
                    if (cryptoTraderStats.Profit > bestProfit)
                    {
                        bestSettings = strategy.Settings;
                        bestProfit = cryptoTraderStats.Profit;
                    }

                    lock (_syncObject)
                    {
                        if (dict.Any(d => d.Key == strategy.Settings.ToString()))
                        {
                            if (dict[strategy.Settings.ToString()].Profit < cryptoTraderStats.Profit)
                                dict[strategy.Settings.ToString()] = cryptoTraderStats;
                        }
                        else
                        {
                            dict[strategy.Settings.ToString()] = cryptoTraderStats;
                        }
                    }
                    var percentage = (it * 100) / totalIterations;
                    if (percentage != oldPercentage)
                    {
                        oldPercentage = percentage;
                        Console.WriteLine($"{bestProfit}%\t\t{percentage}%\t\t{bestSettings.ToString()}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });

            var topSettings = dict.OrderByDescending(d => d.Value.Profit).Take(50).ToList();
            foreach (var keyValuePair in topSettings)
            {
                Console.WriteLine($"{keyValuePair.Value.Profit}% - {keyValuePair.Key}\t{keyValuePair.Value.Opened}\\{keyValuePair.Value.Closed}\t{keyValuePair.Value.InvestedBTC}\t{keyValuePair.Value.CurrentBTC}");
            }
            Console.WriteLine($"Best settings {bestSettings.StopLoss}\t{bestProfit} BTC");
            return new BacktestingStats
            {
                Market = market,
                TradingStrategy = new HoldUntilPriceDropsStrategy { Settings = bestSettings },
                TraderStats = topSettings[0].Value,
                TraderSettings = bestSettings
            };
        }

    }
}
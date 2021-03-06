﻿using CryBot.Core.Trader;
using CryBot.Core.Strategies;
using CryBot.Core.Exchange.Models;
using CryBot.UnitTests.Infrastructure;

using FluentAssertions;

using Moq;

using System.Linq;

using System.Threading.Tasks;
using System.Collections.Generic;

using Xunit;

namespace CryBot.UnitTests.Services.CoinTraderTests
{
    public class TradingTests : CoinTraderTestBase
    {
        private readonly Ticker _newPriceTicker = new Ticker { Ask = 100, Bid = 120 };

        [Fact]
        public async Task CoinTrader_Should_BeAbleToReceivePriceUpdates()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell });

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CoinTrader.Ticker.Ask.Should().Be(100);
        }

        [Fact]
        public async Task PriceUpdate_Should_UpdateTraderProfit()
        {
            CryptoApiMock.MockBuyingTrade(new CryptoOrder());
            await CoinTrader.StartAsync();
            var trade = new Trade();
            trade.BuyOrder.PricePerUnit = 100;
            CoinTrader.Trades = new List<Trade> { trade };
            trade.Status = TradeStatus.Bought;
            CoinTrader.Strategy = new HoldUntilPriceDropsStrategy();
            await CoinTrader.UpdatePrice(_newPriceTicker);

            CoinTrader.Trades[0].Profit.Should().Be(19.4M);
        }

        [Fact]
        public async Task CoinTrader_Should_CallStrategyWhenPriceIsUpdated()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell });
            await CoinTrader.UpdatePrice(_newPriceTicker);

            Strategy.Verify(s => s.CalculateTradeAction(It.Is<Ticker>(t => t.Ask == 100), It.IsAny<Trade>()), Times.Once);
        }

        [Fact]
        public async Task BuyAdvice_Should_CreateBuyOrder()
        {
            CryptoApiMock.MockBuyingTrade(new CryptoOrder());
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Buy, OrderPricePerUnit = 98 });

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CryptoApiMock.Verify(c => c.BuyCoinAsync(It.Is<CryptoOrder>(b => b.PricePerUnit == 98)), Times.Once);
        }

        [Fact]
        public async Task SellAdvice_Should_CreateSellOrder()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell, OrderPricePerUnit = 120 });

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CryptoApiMock.Verify(c => c.SellCoinAsync(It.Is<CryptoOrder>(b => b.PricePerUnit == 120)), Times.Once);
        }

        [Fact]
        public async Task CancelAdvice_Should_CancelBuyOrder()
        {
            CryptoApiMock.MockCancelTrade(new CryptoOrder());
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Cancel });

            CoinTrader.Trades[0].BuyOrder.Uuid = "test";
            await CoinTrader.UpdatePrice(_newPriceTicker);

            CryptoApiMock.Verify(c => c.CancelOrder(It.Is<string>(s => s == "test")), Times.Once);
        }

        [Fact]
        public async Task UpdatePrice_Should_UpdateAllTrades()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Cancel });
            CryptoApiMock.MockCancelTrade(new CryptoOrder());
            CoinTrader.Trades.Add(new Trade());

            await CoinTrader.UpdatePrice(_newPriceTicker);

            Strategy.Verify(s => s.CalculateTradeAction(It.IsAny<Ticker>(), It.IsAny<Trade>()), Times.Exactly(2));
        }

        [Fact]
        public async Task SellOrder_Should_CreateEmptyTrade()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell });

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CoinTrader.Trades.Count.Should().Be(2);
            CoinTrader.Trades[1].Status.Should().Be(TradeStatus.Empty);
        }

        [Fact]
        public async Task MultipleSells_ShouldAdd_TradeForEachOne()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell });
            CoinTrader.Trades.Add(new Trade());

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CoinTrader.Trades.Count.Should().Be(4);
            CoinTrader.Trades[2].Status.Should().Be(TradeStatus.Empty);
            CoinTrader.Trades[3].Status.Should().Be(TradeStatus.Empty);
        }

        [Fact]
        public async Task CompletedTrades_ShouldNot_BeUpdated()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell });
            CoinTrader.Trades[0].Status = TradeStatus.Completed;

            await CoinTrader.UpdatePrice(_newPriceTicker);

            Strategy.Verify(s => s.CalculateTradeAction(It.IsAny<Ticker>(), It.IsAny<Trade>()), Times.Never);
        }

        [Fact]
        public async Task SoldCoin_Should_UpdateTradeStatus()
        {
            var sellOrder = new CryptoOrder { OrderType = CryptoOrderType.LimitSell, Price = 1100, Uuid = "S" };
            var trade = new Trade
            {
                SellOrder = sellOrder
            };

            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell });
            CoinTrader.Trades.Add(trade);
            await CoinTrader.UpdateOrder(sellOrder);

            trade.Status.Should().Be(TradeStatus.Completed);
        }

        [Fact]
        public async Task SellingCoin_Should_UpdateTradeStatusIfOrderIsSuccessful()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell });

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CoinTrader.Trades[0].Status.Should().Be(TradeStatus.Selling);
        }
        
        [Fact]
        public async Task CancellingBuyOrder_Should_RemoveTrade()
        {
            CryptoApiMock.MockCancelTrade(new CryptoOrder());
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Cancel });

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CoinTrader.Trades.Count.Should().Be(0);
        }

        [Fact]
        public async Task EmptyTradesList_Should_AddNewTrade()
        {
            CryptoApiMock.MockBuyingTrade(new CryptoOrder());
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Buy, OrderPricePerUnit = 98 });
            CoinTrader.Trades = new List<Trade>();

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CoinTrader.Trades.Count.Should().Be(1);
        }

        [Fact]
        public async Task BoughtOrder_Should_UpdateTraderStatus()
        {
            var trade = new Trade();
            await CoinTrader.StartAsync();
            CoinTrader.Trades = new List<Trade> { trade };
            trade.BuyOrder.Uuid = "B";
            var buyOrder = new CryptoOrder
            {
                Uuid = "B",
                OrderType = CryptoOrderType.LimitBuy
            };

            await CoinTrader.UpdateOrder(buyOrder);

            CoinTrader.Trades[0].Status.Should().Be(TradeStatus.Bought);
        }

        [Fact]
        public async Task BuyingOrder_Should_UpdateTraderStatus()
        {
            CryptoApiMock.MockBuyingTrade(new CryptoOrder());
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Buy, OrderPricePerUnit = 98 });

            await CoinTrader.UpdatePrice(_newPriceTicker);
            CoinTrader.Trades[0].Status.Should().Be(TradeStatus.Buying);
        }

        [Fact]
        public async Task SellingCoin_Should_UpdateOrderForTrade()
        {
            await InitializeTrader(new TradeAction { TradeAdvice = TradeAdvice.Sell, OrderPricePerUnit = 98 });
            var sellOrder = new CryptoOrder { OrderType = CryptoOrderType.LimitSell, Price = 1100, Uuid = "S" };
            CryptoApiMock.MockSellingTrade(sellOrder);
            CoinTrader.Trades[0].SellOrder.Uuid = "S";

            await CoinTrader.UpdatePrice(_newPriceTicker);

            CoinTrader.Trades[0].SellOrder.Uuid.Should().Be("S");
            CoinTrader.Trades[0].SellOrder.Price.Should().Be(1100);
        }
    }
}

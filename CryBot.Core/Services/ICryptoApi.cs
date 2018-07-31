﻿using Bittrex.Net.Objects;

using CryBot.Contracts;
using CryBot.Core.Models;

using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CryBot.Core.Services
{
    public interface ICryptoApi
    {
        event EventHandler<List<Ticker>> MarketsUpdated;

        event EventHandler<CryptoOrder> OrderUpdated;

        void Initialize(string apiKey, string apiSecret);

        Task<CryptoResponse<Wallet>> GetWalletAsync();

        Task<CryptoResponse<List<CryptoOrder>>> GetOpenOrdersAsync();

        Task<CryptoResponse<List<CryptoOrder>>> GetCompletedOrdersAsync();

        Task<CryptoResponse<CryptoOrder>> BuyCoinAsync(CryptoOrder buyOrder);

        Task<CryptoResponse<Ticker>> GetTickerAsync(string market);

        Task<CryptoResponse<CryptoOrder>> SellCoinAsync(CryptoOrder sellOrder);

        Task<CryptoResponse<List<Market>>> GetMarketsAsync();
        Task<CryptoResponse<List<Candle>>> GetCandlesAsync(string market, TickInterval interval);
    }
}
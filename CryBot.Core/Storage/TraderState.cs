﻿using CryBot.Core.Trader;
using CryBot.Core.Strategies;
using CryBot.Core.Exchange.Models;

using System.Collections.Generic;

namespace CryBot.Core.Storage
{
    public class TraderState
    {
        public List<Trade> Trades { get; set; }
        
        public string Market { get; set; }
        
        public Ticker CurrentTicker { get; set; }
        
        public TraderSettings Settings { get; set; }
    }
}
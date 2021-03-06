﻿import * as signalR from '@aspnet/signalr';
import { HttpClient } from 'aurelia-fetch-client';
import { inject } from 'aurelia-framework';
import { ITrader } from "../../models/api/ITrader";
import { ITraderResponse } from "../../models/api/ITraderResponse";
import { Ticker } from '../../models/api/Ticker';

@inject(HttpClient)
export class Trader {
    traderData: ITrader;
    profit: number = 0;
    market: string = "BTC-ETC";
    private connectionPromise?: Promise<void>;
    private chatHubConnection: signalR.HubConnection;

    tickerLog: Ticker[] = [];
    currentTicker: Ticker = new Ticker();

    constructor(http: HttpClient) {
        this.httpClient = http;
    }

    activate(traderData: ITrader) {
        this.traderData = traderData;
        this.market = this.traderData.market;
        this.chatHubConnection = new signalR.HubConnectionBuilder().withUrl("/app").build();

        this.chatHubConnection.on('traderUpdate:' + this.market, (trader: ITrader) => {
            if (trader) {
                this.traderData = trader;
                this.calculateProfit();
            }
        });
        this.chatHubConnection.on('priceUpdate:' + this.market, (newTicker: Ticker) => {
            this.traderData.currentTicker = newTicker;
            this.tickerLog.unshift(newTicker);
            this.calculateProfit();
        });
        this.httpClient.fetch('api/traders?market=' + this.market)
            .then(result => result.json() as Promise<ITraderResponse>)
            .then(data => {
                if (data.isSuccessful) {
                    this.traderData = data.trader;
                    this.calculateProfit();
                }

            });
        this.connectionPromise = this.chatHubConnection.start();
    }

    deactivate() {
        this.connectionPromise = undefined;
        this.chatHubConnection.stop();
    }

    async sendMessage(): Promise<void> {
        if (!this.connectionPromise) {
            console.warn('Chat: No connection to the server.');
        }
        await this.connectionPromise;
    }

    httpClient: HttpClient;

    calculateProfit(): any {

        this.profit = 0;
        this.traderData.trades.forEach(t => {
            this.profit += t.profit;
        });
        this.profit = Math.round(this.profit * 100) / 100;
    }
}
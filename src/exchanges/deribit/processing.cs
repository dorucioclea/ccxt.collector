﻿using CCXT.Collector.Deribit.Public;
using CCXT.Collector.Library;
using CCXT.Collector.Service;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OdinSdk.BaseLib.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CCXT.Collector.Deribit
{
    public partial class Processing
    {
        private static ConcurrentQueue<QMessage> __recv_queue = null;

        /// <summary>
        ///
        /// </summary>
        private static ConcurrentQueue<QMessage> ReceiveQ
        {
            get
            {
                if (__recv_queue == null)
                    __recv_queue = new ConcurrentQueue<QMessage>();

                return __recv_queue;
            }
        }
        
        private readonly DRConfig __drconfig;

        public Processing(IConfiguration configuration)
        {
            __drconfig = new DRConfig(configuration);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        public static void SendReceiveQ(QMessage message)
        {
            ReceiveQ.Enqueue(message);
        }

        public async Task Start(CancellationToken cancelToken)
        {
            DRLogger.SNG.WriteO(this, $"processing service start...");

            var _processing = Task.Run(async () =>
            {
                var _last_polling_trade = 0L;

                while (true)
                {
                    try
                    {
                        await Task.Delay(0);

                        var _message = (QMessage)null;
                        if (ReceiveQ.TryDequeue(out _message) == false)
                        {
                            var _cancelled = cancelToken.WaitHandle.WaitOne(0);
                            if (_cancelled == true)
                                break;

                            await Task.Delay(10);
                            continue;
                        }

                        if (_message.command == "WS")
                        {
                            if (_message.stream == "trade")
                            {
                                var _w_trades = JsonConvert.DeserializeObject<List<DCompleteOrderItem>>(_message.payload ?? "");

                                var _s_trade = new SCompleteOrders
                                {
                                    exchange = _message.exchange,
                                    stream = _message.stream,
                                    symbol = _message.symbol,
                                    action = _message.action,
                                    sequentialId = _w_trades.Max(t => t.timestamp),

                                    result = _w_trades.Select(t =>
                                    {
                                        return new SCompleteOrderItem
                                        {
                                            timestamp = t.timestamp,
                                            sideType = t.sideType,
                                            price = t.price,
                                            quantity = t.quantity
                                        };
                                    })
                                    .ToList()
                                };

                                if (_s_trade.result.Count() > 0)
                                {
                                    _last_polling_trade = _s_trade.sequentialId;
                                    await mergeCompleteOrder(_s_trade);
                                }
                            }
                            else if (_message.stream == "orderbook")
                            {
                                var _w_orderbooks = JsonConvert.DeserializeObject<DRResults<DOrderBook>>(_message.payload ?? "");

                                var _timestamp = _w_orderbooks.result.timestamp;
                                var _asks = _w_orderbooks.result.asks;
                                var _bids = _w_orderbooks.result.bids;

                                var _s_orderbooks = new SOrderBooks
                                {
                                    exchange = _message.exchange,
                                    symbol = _message.symbol,
                                    stream = _message.stream,
                                    action = _message.action,
                                    sequentialId = _timestamp,

                                    result = new SOrderBook
                                    {
                                        timestamp = _timestamp,
                                        askSumQty = _asks.Sum(o => o.quantity),
                                        bidSumQty = _bids.Sum(o => o.quantity),

                                        asks = _asks.Select(o =>
                                        {
                                            return new SOrderBookItem
                                            {
                                                quantity = o.quantity,
                                                price = o.price,
                                                amount = o.quantity * o.price,
                                                id = 0,
                                                count = 1
                                            };
                                        }).ToList(),
                                        bids = _bids.Select(o =>
                                        {
                                            return new SOrderBookItem
                                            {
                                                quantity = o.quantity,
                                                price = o.price,
                                                amount = o.quantity * o.price,
                                                id = 0,
                                                count = 1
                                            };
                                        }).ToList()
                                    }
                                };

                                await mergeOrderbook(_s_orderbooks);
                            }
                        }
                        else if (_message.command == "AP")
                        {
                            if (_message.stream == "trade")
                            {
                                var _a_trades = JsonConvert.DeserializeObject<DRResults<DCompleteOrders>>(_message.payload ?? "");
                                if (_a_trades.result.trades.Count > 0)
                                {
                                    var _s_trade = new SCompleteOrders
                                    {
                                        exchange = _message.exchange,
                                        symbol = _message.symbol,
                                        stream = _message.stream,
                                        action = _message.action,
                                        sequentialId = _a_trades.result.trades.Max(t => t.timestamp),

                                        result = _a_trades.result.trades.Where(t => t.timestamp > _last_polling_trade).Select(t =>
                                        {
                                            return new SCompleteOrderItem
                                            {
                                                timestamp = t.timestamp,
                                                sideType = t.sideType,
                                                price = t.price,
                                                quantity = t.quantity
                                            };
                                        })
                                      .ToList()
                                    };

                                    if (_s_trade.result.Count() > 0)
                                    {
                                        _last_polling_trade = _s_trade.sequentialId;
                                        await mergeCompleteOrder(_s_trade);
                                    }
                                }
                            }
                            else if (_message.stream == "orderbook")
                            {
                                var _a_orderbooks = JsonConvert.DeserializeObject<DRResults<DOrderBook>>(_message.payload ?? "");
                                if (_a_orderbooks.result.asks.Count > 0 || _a_orderbooks.result.bids.Count > 0)
                                {
                                    var _timestamp = _a_orderbooks.result.timestamp;
                                    var _asks = _a_orderbooks.result.asks;
                                    var _bids = _a_orderbooks.result.bids;

                                    var _s_orderbooks = new SOrderBooks
                                    {
                                        exchange = _message.exchange,
                                        symbol = _message.symbol,
                                        stream = _message.stream,
                                        action = _message.action,
                                        sequentialId = _timestamp,

                                        result = new SOrderBook
                                        {
                                            timestamp = _timestamp,
                                            askSumQty = _asks.Sum(o => o.quantity),
                                            bidSumQty = _bids.Sum(o => o.quantity),

                                            asks = _asks.Select(o =>
                                            {
                                                return new SOrderBookItem
                                                {
                                                    quantity = o.quantity,
                                                    price = o.price,
                                                    amount = o.quantity * o.price,
                                                    id = 0,
                                                    count = 1
                                                };
                                            }).ToList(),
                                            bids = _bids.Select(o =>
                                            {
                                                return new SOrderBookItem
                                                {
                                                    quantity = o.quantity,
                                                    price = o.price,
                                                    amount = o.quantity * o.price,
                                                    id = 0,
                                                    count = 1
                                                };
                                            }).ToList()
                                        }
                                    };

                                    await mergeOrderbook(_s_orderbooks);
                                }
                            }
                        }
                        else if (_message.command == "SS")
                        {
                            await snapshotOrderbook(_message.exchange);
                        }
#if DEBUG
                        else
                            DRLogger.SNG.WriteO(this, _message.payload);
#endif
                        if (cancelToken.IsCancellationRequested == true)
                            break;
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        DRLogger.SNG.WriteX(this, ex.ToString());
                    }
                }
            },
            cancelToken
            );

            await Task.WhenAll(_processing);

            DRLogger.SNG.WriteO(this, $"processing service stop...");
        }
    }
}
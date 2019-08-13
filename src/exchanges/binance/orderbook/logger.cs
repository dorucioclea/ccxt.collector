﻿using CCXT.Collector.Library.Service;

namespace CCXT.Collector.Binance.Orderbook
{
    public class BLogger
    {
        public const string exchange_name = "binance";

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        public static void WriteQ(string message)
        {
            LoggerQ.WriteQ(message, exchange_name);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        public static void WriteO(string message)
        {
            LoggerQ.WriteO(message, exchange_name);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        public static void WriteX(string message)
        {
            LoggerQ.WriteX(message, exchange_name);
        }
    }
}
﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using System;
using System.Text;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusStats
    {
        private CoinViewStack stack;
        private CachedCoinView cache;
        private DBreezeCoinView dbreeze;
        private CoinView bottom;

        private LookaheadBlockPuller lookaheadPuller;
        private ConsensusPerformanceSnapshot lastSnapshot;
        private BackendPerformanceSnapshot lastSnapshot2;
        private CachePerformanceSnapshot lastSnapshot3;
        private readonly ConsensusLoop consensusLoop;
        private readonly ChainBehavior.ChainState chainState;
        private readonly ConcurrentChain chain;
        private readonly IConnectionManager connectionManager;

        public ConsensusStats(CoinViewStack stack, CoinView coinView, ConsensusLoop consensusLoop, ChainBehavior.ChainState chainState, ConcurrentChain chain, IConnectionManager connectionManager)
        {
            stack = new CoinViewStack(coinView);
            this.cache = stack.Find<CachedCoinView>();
            this.dbreeze = stack.Find<DBreezeCoinView>();
            this.bottom = stack.Bottom;

            this.consensusLoop = consensusLoop;
            this.lookaheadPuller = this.consensusLoop.Puller as LookaheadBlockPuller;

            this.lastSnapshot = consensusLoop.Validator.PerformanceCounter.Snapshot();
            this.lastSnapshot2 = this.dbreeze?.PerformanceCounter.Snapshot();
            this.lastSnapshot3 = this.cache?.PerformanceCounter.Snapshot();
            this.chainState = chainState;
            this.chain = chain;
            this.connectionManager = connectionManager;
        }

        public bool CanLog
        {
            get
            {
                return this.chainState.IsInitialBlockDownload && (DateTimeOffset.UtcNow - this.lastSnapshot.Taken) > TimeSpan.FromSeconds(5.0);
            }
        }

        public void Log()
        {
            StringBuilder benchLogs = new StringBuilder();

            if (this.lookaheadPuller != null)
            {
                benchLogs.AppendLine("======Block Puller======");
                benchLogs.AppendLine("Lookahead:".PadRight(Logs.ColumnLength) + this.lookaheadPuller.ActualLookahead + " blocks");
                benchLogs.AppendLine("Downloaded:".PadRight(Logs.ColumnLength) + this.lookaheadPuller.MedianDownloadCount + " blocks");
                benchLogs.AppendLine("==========================");
            }
            benchLogs.AppendLine("Persistent Tip:".PadRight(Logs.ColumnLength) + this.chain.GetBlock(this.bottom.GetBlockHashAsync().Result).Height);
            if (this.cache != null)
            {
                benchLogs.AppendLine("Cache Tip".PadRight(Logs.ColumnLength) + this.chain.GetBlock(this.cache.GetBlockHashAsync().Result).Height);
                benchLogs.AppendLine("Cache entries".PadRight(Logs.ColumnLength) + this.cache.CacheEntryCount);
            }

            var snapshot = this.consensusLoop.Validator.PerformanceCounter.Snapshot();
            benchLogs.AppendLine((snapshot - this.lastSnapshot).ToString());
            this.lastSnapshot = snapshot;

            if (this.dbreeze != null)
            {
                var snapshot2 = this.dbreeze.PerformanceCounter.Snapshot();
                benchLogs.AppendLine((snapshot2 - this.lastSnapshot2).ToString());
                this.lastSnapshot2 = snapshot2;
            }
            if (this.cache != null)
            {
                var snapshot3 = this.cache.PerformanceCounter.Snapshot();
                benchLogs.AppendLine((snapshot3 - this.lastSnapshot3).ToString());
                this.lastSnapshot3 = snapshot3;
            }
            benchLogs.AppendLine(this.connectionManager.GetStats());
            Logs.Bench.LogInformation(benchLogs.ToString());
        }
    }
}

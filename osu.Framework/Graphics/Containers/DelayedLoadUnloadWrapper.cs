﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Statistics;
using System.Diagnostics;
using osu.Framework.Threading;

namespace osu.Framework.Graphics.Containers
{
    public class DelayedLoadUnloadWrapper : DelayedLoadWrapper
    {
        private readonly Func<Drawable> createContentFunction;
        private readonly double timeBeforeUnload;

        public DelayedLoadUnloadWrapper(Func<Drawable> createContentFunction, double timeBeforeLoad = 500, double timeBeforeUnload = 1000)
            : base(createContentFunction(), timeBeforeLoad)
        {
            this.createContentFunction = createContentFunction;
            this.timeBeforeUnload = timeBeforeUnload;
        }

        private static readonly GlobalStatistic<int> loaded_count = GlobalStatistics.Get<int>("Drawable", $"{nameof(DelayedLoadUnloadWrapper)}s loaded");

        private double timeHidden;

        private ScheduledDelegate unloadSchedule;

        protected bool ShouldUnloadContent => timeBeforeUnload == 0 || timeHidden > timeBeforeUnload;

        private double lifetimeStart = double.MinValue;

        public override double LifetimeStart
        {
            get => base.Content?.LifetimeStart ?? lifetimeStart;
            set
            {
                if (base.Content != null)
                    base.Content.LifetimeStart = value;
                lifetimeStart = value;
            }
        }

        private double lifetimeEnd = double.MaxValue;

        public override double LifetimeEnd
        {
            get => base.Content?.LifetimeEnd ?? lifetimeEnd;
            set
            {
                if (base.Content != null)
                    base.Content.LifetimeEnd = value;
                lifetimeEnd = value;
            }
        }

        public override Drawable Content => base.Content ?? (Content = createContentFunction());

        protected override void EndDelayedLoad(Drawable content)
        {
            base.EndDelayedLoad(content);

            content.LifetimeStart = lifetimeStart;
            content.LifetimeEnd = lifetimeEnd;

            Debug.Assert(unloadSchedule == null);

            if (OptimisingContainer != null)
            {
                unloadSchedule = OptimisingContainer.ScheduleCheckAction(checkForUnload);
                Debug.Assert(unloadSchedule != null);
                loaded_count.Value++;
            }
        }

        protected override void CancelTasks()
        {
            base.CancelTasks();

            if (unloadSchedule != null)
            {
                unloadSchedule.Cancel();
                unloadSchedule = null;

                loaded_count.Value--;
            }
        }

        private void checkForUnload()
        {
            // This code can be expensive, so only run if we haven't yet loaded.
            if (IsIntersecting)
                timeHidden = 0;
            else
                timeHidden += Time.Elapsed;

            if (ShouldUnloadContent)
            {
                ClearInternal();
                Content = null;

                timeHidden = 0;

                CancelTasks();
            }
        }
    }
}

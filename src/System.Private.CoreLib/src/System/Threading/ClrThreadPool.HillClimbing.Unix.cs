namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        // Config values pulled from CoreCLR
        private static readonly HillClimbing s_threadPoolHillClimber = new HillClimbing(4, 20, 100, 8, 15, 300, 4, 20, 10, 200, 1, 200, 15);

        private class HillClimbing
        {
            public enum State
            {
                Warmup,
                Initializing,
                RandomMove,
                ClimbingMove,
                ChangePoint,
                Stabilizing,
                Starvation,
                ThreadTimedOut,
            }

            private State _currentState;

            private readonly int _wavePeriod;
            private readonly int _samplesToMeasure;
            private readonly double _targetThroughputRatio;
            private readonly double _targetSignalToNoiseRatio;
            private readonly double _maxChangePerSecond;
            private readonly double _maxChangePerSample;
            private readonly int _maxThreadWaveMagnitude;
            private readonly int _sampleIntervalLow;
            private readonly double _threadMagnitudeMultiplier;
            private readonly int _sampleIntervalHigh;
            private readonly double _throughputErrorSmoothingFactor;
            private readonly double _gainExponent;
            private readonly double _maxSampleError;

            private double _currentControlSetting;
            private int _totalSamples;
            private int _lastThreadCount;
            private double _averageThroughputNoise;
            private double _secondsElapsedSinceLastChange;
            private double _completionsSinceLastChange;
            private int _accumulatedCompletionCount;
            private double _accumulatedSampleDuration;
            private double[] _samples;
            private double[] _threadCounts;
            private int _currentSampleInterval;
            
            private Random _randomIntervalGenerator = new Random();

            public HillClimbing(int wavePeriod, int maxWaveMagnitude, double waveMagnitudeMultiplier, int waveHistorySize, double targetThroughputRatio,
                double targetSignalToNoiseRatio, double maxChangePerSecond, double maxChangePerSample, int sampleIntervalLow, int sampleIntervalHigh,
                double errorSmoothingFactor, double gainExponent, double maxSampleError)
            {
                _wavePeriod = wavePeriod;
                _maxThreadWaveMagnitude = maxWaveMagnitude;
                _threadMagnitudeMultiplier = waveMagnitudeMultiplier / 100.0;
                _samplesToMeasure = wavePeriod * waveHistorySize;
                _targetThroughputRatio = targetThroughputRatio / 100.0;
                _targetSignalToNoiseRatio = targetSignalToNoiseRatio / 100.0;
                _maxChangePerSecond = maxChangePerSecond;
                _maxChangePerSample = maxChangePerSample;
                _sampleIntervalLow = sampleIntervalLow;
                _sampleIntervalHigh = sampleIntervalHigh;
                _throughputErrorSmoothingFactor = errorSmoothingFactor / 100.0;
                _gainExponent = gainExponent / 100.0;
                _maxSampleError = maxSampleError / 100.0;

                _currentSampleInterval = _randomIntervalGenerator.Next(_sampleIntervalLow, _sampleIntervalHigh);
            }

            public (int newThreadCount, int newSampleInterval) Update(int currentThreadCount, double sampleDuration, int numCompletions)
            {

                //
                // If someone changed the thread count without telling us, update our records accordingly.
                // 
                if (currentThreadCount != _lastThreadCount)
                    ForceChange(currentThreadCount, State.Initializing);

                //
                // Update the cumulative stats for this thread count
                //
                _secondsElapsedSinceLastChange += sampleDuration;
                _completionsSinceLastChange += numCompletions;

                //
                // Add in any data we've already collected about this sample
                //
                sampleDuration += _accumulatedSampleDuration;
                numCompletions += _accumulatedCompletionCount;

                //
                // We need to make sure we're collecting reasonably accurate data.  Since we're just counting the end
                // of each work item, we are goinng to be missing some data about what really happened during the 
                // sample interval.  The count produced by each thread includes an initial work item that may have 
                // started well before the start of the interval, and each thread may have been running some new 
                // work item for some time before the end of the interval, which did not yet get counted.  So
                // our count is going to be off by +/- threadCount workitems.
                //
                // The exception is that the thread that reported to us last time definitely wasn't running any work
                // at that time, and the thread that's reporting now definitely isn't running a work item now.  So
                // we really only need to consider threadCount-1 threads.
                //
                // Thus the percent error in our count is +/- (threadCount-1)/numCompletions.
                //
                // We cannot rely on the frequency-domain analysis we'll be doing later to filter out this error, because
                // of the way it accumulates over time.  If this sample is off by, say, 33% in the negative direction,
                // then the next one likely will be too.  The one after that will include the sum of the completions
                // we missed in the previous samples, and so will be 33% positive.  So every three samples we'll have 
                // two "low" samples and one "high" sample.  This will appear as periodic variation right in the frequency
                // range we're targeting, which will not be filtered by the frequency-domain translation.
                //
                if (_totalSamples > 0 && ((currentThreadCount - 1.0) / numCompletions) >= _maxSampleError)
                {
                    // not accurate enough yet.  Let's accumulate the data so far, and tell the ThreadPool
                    // to collect a little more.
                    _accumulatedSampleDuration = sampleDuration;
                    _accumulatedCompletionCount = numCompletions;
                    return (currentThreadCount, 10);
                }

                //
                // We've got enouugh data for our sample; reset our accumulators for next time.
                //
                _accumulatedSampleDuration = 0;
                _accumulatedCompletionCount = 0;

                //
                // Add the current thread count and throughput sample to our history
                //
                double throughput = numCompletions / sampleDuration;
                //Worker Thread Adjustment Sample event

                int sampleIndex = _totalSamples % _samplesToMeasure;
                _samples[sampleIndex] = throughput;
                _threadCounts[sampleIndex] = currentThreadCount;
                _totalSamples++;

                //
                // Set up defaults for our metrics
                //
                (double real, double imaginary) threadWaveComponent = (0, 0);
                (double real, double imaginary) throughputWaveComponent = (0, 0);
                double throughputErrorEstimate = 0;
                (double real, double imaginary) ratio = (0, 0);
                double confidence = 0;

                State state = State.Warmup;

                //
                // How many samples will we use?  It must be at least the three wave periods we're looking for, and it must also be a whole
                // multiple of the primary wave's period; otherwise the frequency we're looking for will fall between two  frequency bands 
                // in the Fourier analysis, and we won't be able to measure it accurately.
                // 
                int sampleCount = Math.Min(_totalSamples - 1, _samplesToMeasure) / _wavePeriod * _wavePeriod;

                if (sampleCount > _wavePeriod)
                {
                    //
                    // Average the throughput and thread count samples, so we can scale the wave magnitudes later.
                    //
                    double sampleSum = 0;
                    double threadSum = 0;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        sampleSum += _samples[(_totalSamples - sampleCount + i) % _samplesToMeasure];
                        threadSum += _threadCounts[(_totalSamples - sampleCount + i) % _samplesToMeasure];
                    }
                    double averageThroughput = sampleSum / sampleCount;
                    double averageThreadCount = threadSum / sampleCount;

                    if (averageThroughput > 0 && averageThreadCount > 0)
                    {
                        //
                        // Calculate the periods of the adjacent frequency bands we'll be using to measure noise levels.
                        // We want the two adjacent Fourier frequency bands.
                        //
                        double adjacentPeriod1 = sampleCount / (((double)sampleCount / _wavePeriod) + 1);
                        double adjacentPeriod2 = sampleCount / (((double)sampleCount / _wavePeriod) - 1);

                        //
                        // Get the the three different frequency components of the throughput (scaled by average
                        // throughput).  Our "error" estimate (the amount of noise that might be present in the
                        // frequency band we're really interested in) is the average of the adjacent bands.
                        //
                        throughputWaveComponent = GetWaveComponent(_samples, sampleCount, _wavePeriod);
                        throughputWaveComponent = (throughputWaveComponent.real / averageThroughput, throughputWaveComponent.imaginary / averageThroughput);
                        (double real, double imaginary) intermediateErrorEstimate = GetWaveComponent(_samples, sampleCount, adjacentPeriod1);
                        throughputErrorEstimate = Abs((intermediateErrorEstimate.real / averageThroughput, intermediateErrorEstimate.imaginary / averageThroughput));
                        if (adjacentPeriod2 <= sampleCount)
                        {
                            intermediateErrorEstimate = GetWaveComponent(_samples, sampleCount, adjacentPeriod2);
                            throughputErrorEstimate = Math.Max(throughputErrorEstimate, Abs((intermediateErrorEstimate.real / averageThroughput, intermediateErrorEstimate.imaginary / averageThroughput)));
                        }

                        //
                        // Do the same for the thread counts, so we have something to compare to.  We don't measure thread count
                        // noise, because there is none; these are exact measurements.
                        //
                        (double real, double imaginary) intermediateThreadWaveComponent = GetWaveComponent(_threadCounts, sampleCount, _wavePeriod);
                        threadWaveComponent = (intermediateThreadWaveComponent.real / averageThreadCount, intermediateThreadWaveComponent.imaginary / averageThreadCount);

                        //
                        // Update our moving average of the throughput noise.  We'll use this later as feedback to
                        // determine the new size of the thread wave.
                        //
                        if (_averageThroughputNoise == 0)
                            _averageThroughputNoise = throughputErrorEstimate;
                        else
                            _averageThroughputNoise = (_throughputErrorSmoothingFactor * throughputErrorEstimate) + ((1.0 - _throughputErrorSmoothingFactor) * _averageThroughputNoise);

                        if (Abs(threadWaveComponent) > 0)
                        {
                            //
                            // Adjust the throughput wave so it's centered around the target wave, and then calculate the adjusted throughput/thread ratio.
                            //
                            ratio.real = (throughputWaveComponent.real - (_targetThroughputRatio * threadWaveComponent.real)) / threadWaveComponent.real;
                            ratio.imaginary = (throughputWaveComponent.imaginary - (_targetThroughputRatio * threadWaveComponent.imaginary)) / threadWaveComponent.imaginary;
                            state = State.ClimbingMove;
                        }
                        else
                        {
                            ratio = (0, 0);
                            state = State.Stabilizing;
                        }

                        //
                        // Calculate how confident we are in the ratio.  More noise == less confident.  This has
                        // the effect of slowing down movements that might be affected by random noise.
                        //
                        double noiseForConfidence = Math.Max(_averageThroughputNoise, throughputErrorEstimate);
                        if (noiseForConfidence > 0)
                            confidence = (Abs(threadWaveComponent) / noiseForConfidence) / _targetSignalToNoiseRatio;
                        else
                            confidence = 1.0; //there is no noise!

                    }
                }

                //
                // We use just the real part of the complex ratio we just calculated.  If the throughput signal
                // is exactly in phase with the thread signal, this will be the same as taking the magnitude of
                // the complex move and moving that far up.  If they're 180 degrees out of phase, we'll move
                // backward (because this indicates that our changes are having the opposite of the intended effect).
                // If they're 90 degrees out of phase, we won't move at all, because we can't tell wether we're
                // having a negative or positive effect on throughput.
                //
                double move = Math.Min(1.0, Math.Max(-1.0, ratio.real));

                //
                // Apply our confidence multiplier.
                //
                move *= Math.Min(1.0, Math.Max(0.0, confidence));

                //
                // Now apply non-linear gain, such that values around zero are attenuated, while higher values
                // are enhanced.  This allows us to move quickly if we're far away from the target, but more slowly
                // if we're getting close, giving us rapid ramp-up without wild oscillations around the target.
                // 
                double gain = _maxChangePerSecond * sampleDuration;
                move = Math.Pow(Math.Abs(move), _gainExponent) * (move >= 0.0 ? 1 : -1) * gain;
                move = Math.Min(move, _maxChangePerSample);

                //
                // If the result was positive, and CPU is > 95%, refuse the move.
                //
                if (move > 0.0 && s_cpuUtilization > CpuUtilizationHigh)
                    move = 0.0;

                //
                // Apply the move to our control setting
                // 
                _currentControlSetting += move;

                //
                // Calculate the new thread wave magnitude, which is based on the moving average we've been keeping of
                // the throughput error.  This average starts at zero, so we'll start with a nice safe little wave at first.
                //
                int newThreadWaveMagnitude = (int)(0.5 + (_currentControlSetting * _averageThroughputNoise * _targetSignalToNoiseRatio * _threadMagnitudeMultiplier * 2.0));
                newThreadWaveMagnitude = Math.Min(newThreadWaveMagnitude, _maxThreadWaveMagnitude);
                newThreadWaveMagnitude = Math.Max(newThreadWaveMagnitude, 1);

                //
                // Make sure our control setting is within the ThreadPool's limits
                // 
                int maxThreads = s_maxThreads;
                int minThreads = s_minThreads;

                _currentControlSetting = Math.Min(maxThreads - newThreadWaveMagnitude, _currentControlSetting);
                _currentControlSetting = Math.Max(minThreads, _currentControlSetting);

                //
                // Calculate the new thread count (control setting + square wave)
                // 
                int newThreadCount = (int)(_currentControlSetting + newThreadWaveMagnitude * ((_totalSamples / (_wavePeriod / 2)) % 2));

                //
                // Make sure the new thread count doesn't exceed the ThreadPool's limits
                // 
                newThreadCount = Math.Min(maxThreads, newThreadCount);
                newThreadCount = Math.Max(minThreads, newThreadCount);

                //
                // Record these numbers for posterity
                //
                
                // Worker Thread Adjustment stats event


                //
                // If all of this caused an actual change in thread count, log that as well.
                // 
                if (newThreadCount != currentThreadCount)
                    ChangeThreadCount(newThreadCount, state);

                //
                // Return the new thread count and sample interval.  This is randomized to prevent correlations with other periodic
                // changes in throughput.  Among other things, this prevents us from getting confused by Hill Climbing instances
                // running in other processes.
                //
                // If we're at minThreads, and we seem to be hurting performance by going higher, we can't go any lower to fix this.  So
                // we'll simply stay at minThreads much longer, and only occasionally try a higher value.
                //
                int newSampleInterval;
                if (ratio.real < 0.0 && newThreadCount == minThreads)
                    newSampleInterval = (int)(0.5 + _currentSampleInterval * (10.0 * Math.Max(-ratio.real, 1.0)));
                else
                    newSampleInterval = _currentSampleInterval;

                return (newThreadCount, newSampleInterval);
            }

            private void ChangeThreadCount(int newThreadCount, State state)
            {
                _lastThreadCount = newThreadCount;
                _currentSampleInterval = _randomIntervalGenerator.Next(_sampleIntervalLow, _sampleIntervalHigh);
                double throughput = _secondsElapsedSinceLastChange > 0 ? _completionsSinceLastChange / _secondsElapsedSinceLastChange : 0;
                LogTransition(newThreadCount, throughput, state);
                _secondsElapsedSinceLastChange = 0;
                _completionsSinceLastChange = 0;
            }

            private void LogTransition(int newThreadCount, double throughput, State state)
            {
                // TODO: Log transitions
            }

            public void ForceChange(int newThreadCount, State state)
            {
                if(_lastThreadCount != newThreadCount)
                {
                    _currentControlSetting += newThreadCount - _lastThreadCount;
                    ChangeThreadCount(newThreadCount, state);
                }
            }

            private static (double real, double imaginary) GetWaveComponent(double[] samples, int numSamples, double period)
            {
                double w = 2 * Math.PI / period;
                double cos = Math.Cos(w);
                double coeff = 2 * cos;
                double q0 = 0, q1 = 0, q2 = 0;
                for(int i = 0; i < numSamples && i < samples.Length; ++i)
                {
                    q0 = coeff * q1 - q2 + samples[i];
                    q2 = q1;
                    q1 = q0;
                }
                return ((q1 - q2 * cos) / samples.Length, (q2 * Math.Sin(w)) / samples.Length);
            }

            private static double Abs((double real, double imaginary) complex)
            {
                return Math.Sqrt(complex.real * complex.real + complex.imaginary * complex.imaginary);
            }
        }
    }
}

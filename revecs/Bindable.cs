namespace revecs
{
    public delegate void ValueChanged<in T>(T previous, T next);

    public abstract class BindableListener : IDisposable
    {
        public abstract void Dispose();
    }

    public class BindableListener<T> : BindableListener
    {
        private readonly WeakReference bindableReference;
        private readonly ValueChanged<T> valueChanged;

        public BindableListener(WeakReference bindableReference, ValueChanged<T> valueChanged)
        {
            this.valueChanged = valueChanged;
            this.bindableReference = bindableReference;
        }

        public override void Dispose()
        {
            if (bindableReference.IsAlive) ((Bindable<T>) bindableReference.Target).Unsubscribe(valueChanged);
        }
    }

    public class Bindable<T> : IDisposable
    {
        private ValueChanged<T> currentListener;

        private T value;

        public Bindable(T defaultValue = default, T initialValue = default)
        {
            this.Default = defaultValue;
            if (EqualityComparer<T>.Default.Equals(initialValue, default) &&
                !EqualityComparer<T>.Default.Equals(defaultValue, default))
                value = defaultValue;
            else
                value = initialValue;
        }

        public T Value
        {
            get => value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(this.value, value))
                    return;
                InvokeOnUpdate(ref value);
            }
        }

        public T Default
        {
            get;
            // should we also do a subscription format when default get changed?
            set;
        }

        protected virtual IEnumerable<ValueChanged<T>> SubscribedListeners { get; set; } = new List<ValueChanged<T>>();

        public virtual void Dispose()
        {
            if (SubscribedListeners is IList<ValueChanged<T>> list)
                list.Clear();
        }

        protected virtual void InvokeOnUpdate(ref T value)
        {
            var list = (List<ValueChanged<T>>) SubscribedListeners;

            int count;
            DisposableArray<ValueChanged<T>> disposable;
            ValueChanged<T>[] array;
            lock (SubscribedListeners)
            {
                count = list.Count;

                disposable = DisposableArray<ValueChanged<T>>.Rent(count, out array);
                list.CopyTo(array);
            }

            using (disposable)
            {
                foreach (var listener in array.AsSpan(0, count))
                {
                    currentListener = listener;
                    listener(this.value, value);
                }

                this.value = value;
            }
        }

        /// <summary>
        ///     Unsubcribe the current listener
        /// </summary>
        public void UnsubscribeCurrent()
        {
            lock (SubscribedListeners)
            {
                if (currentListener != null)
                    ((List<ValueChanged<T>>) SubscribedListeners).Remove(currentListener);
            }
        }

        public bool Unsubscribe(ValueChanged<T> listener)
        {
            lock (SubscribedListeners)
            {
                return ((List<ValueChanged<T>>) SubscribedListeners).Remove(listener);
            }
        }

        public virtual BindableListener Subscribe(in ValueChanged<T> listener, bool invokeNow = false)
        {
            lock (SubscribedListeners)
            {
                if (SubscribedListeners is List<ValueChanged<T>> list)
                {
                    if (!list.Contains(listener))
                        list.Add(listener);
                }
                else
                {
                    throw new InvalidOperationException("You've replaced the list type by something else!");
                }
            }

            if (invokeNow)
                listener(default!, value);

            return new BindableListener<T>(new WeakReference(this, true), listener);
        }

        public void SetDefault()
        {
            Value = Default;
        }
    }

    public readonly struct ReadOnlyBindable<T>
    {
        private readonly Bindable<T> source;

        public T Value => source.Value;
        public T Default => source.Default;

        public ReadOnlyBindable(Bindable<T> source)
        {
            this.source = source;
        }

        public void UnsubscribeCurrent()
        {
            source.UnsubscribeCurrent();
        }

        public bool Unsubscribe(ValueChanged<T> listener)
        {
            return source.Unsubscribe(listener);
        }

        public BindableListener Subscribe(in ValueChanged<T> listener, bool invokeNow = false)
        {
            return source.Subscribe(listener, invokeNow);
        }

        public static implicit operator ReadOnlyBindable<T>(Bindable<T> origin)
        {
            return new ReadOnlyBindable<T>(origin);
        }
    }
}
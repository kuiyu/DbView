using System.Reflection;

namespace DbView.Core.Abstractions
{
    /// <summary>
    /// 值对象抽象基类
    /// 值对象的特点：无标识、不可变、通过属性值判断相等
    /// </summary>
    public abstract class ValueObject : IEquatable<ValueObject>
    {
        /// <summary>
        /// 获取用于相等性比较的属性值集合
        /// </summary>
        protected abstract IEnumerable<object> GetEqualityComponents();

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
                return false;

            var other = (ValueObject)obj;
            return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
        }

        public bool Equals(ValueObject other)
        {
            return Equals((object)other);
        }

        public override int GetHashCode()
        {
            return GetEqualityComponents()
                .Select(x => x != null ? x.GetHashCode() : 0)
                .Aggregate((x, y) => x ^ y);
        }

        public static bool operator ==(ValueObject left, ValueObject right)
        {
            if (ReferenceEquals(left, null) && ReferenceEquals(right, null))
                return true;

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(ValueObject left, ValueObject right)
        {
            return !(left == right);
        }

        /// <summary>
        /// 深拷贝值对象
        /// </summary>
        public ValueObject Copy()
        {
            return (ValueObject)this.MemberwiseClone();
        }

        /// <summary>
        /// 检查值对象是否为空或默认值
        /// </summary>
        public virtual bool IsEmpty()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            return properties.All(p =>
            {
                var value = p.GetValue(this);
                return value == null || value.Equals(GetDefaultValue(p.PropertyType));
            });
        }

        private static object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    /// <summary>
    /// 泛型值对象基类，提供类型安全的比较
    /// </summary>
    public abstract class ValueObject<T> : ValueObject
        where T : ValueObject<T>
    {
        public override bool Equals(object obj)
        {
            var valueObject = obj as T;

            if (ReferenceEquals(valueObject, null))
                return false;

            if (GetType() != obj.GetType())
                return false;

            return base.Equals(valueObject);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(ValueObject<T> a, ValueObject<T> b)
        {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null))
                return true;

            if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
                return false;

            return a.Equals(b);
        }

        public static bool operator !=(ValueObject<T> a, ValueObject<T> b)
        {
            return !(a == b);
        }
    }
}



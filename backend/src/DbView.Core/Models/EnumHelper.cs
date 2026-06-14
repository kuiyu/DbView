using System.Collections;
using System.ComponentModel;
using System.Reflection;

public static class EnumHelper
{
    private static Hashtable enumDesciption;

    static EnumHelper()
    {
        enumDesciption = GetDescriptionContainer();
    }

    private static void AddToEnumDescription(Type enumType)
    {
        try
        {
            enumDesciption.Add(enumType, GetEnumDic(enumType));
        }
        catch (Exception)
        {
        }
    }

    private static string GetDescription(Type enumType, string enumText)
    {
        if (!string.IsNullOrEmpty(enumText))
        {
            if (!enumDesciption.ContainsKey(enumType))
            {
                AddToEnumDescription(enumType);
            }

            object obj = enumDesciption[enumType];
            if (obj == null || string.IsNullOrEmpty(enumText))
            {
                throw new ApplicationException("不存在枚举的描述");
            }

            return ((Dictionary<string, string>)obj)[enumText];
        }

        return null;
    }

    private static Hashtable GetDescriptionContainer()
    {
        enumDesciption = new Hashtable();
        return enumDesciption;
    }

    private static Dictionary<string, string> GetEnumDic(Type enumType)
    {
        Dictionary<string, string> dictionary = new Dictionary<string, string>();
        FieldInfo[] fields = enumType.GetFields();
        foreach (FieldInfo fieldInfo in fields)
        {
            if (fieldInfo.FieldType.IsEnum)
            {
                object[] customAttributes = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);
                if (customAttributes.Length != 0)
                {
                    dictionary.Add(fieldInfo.Name, ((DescriptionAttribute)customAttributes[0]).Description);
                }
            }
        }

        return dictionary;
    }

    private static bool IsIntType(double d)
    {
        return (double)(int)d != d;
    }

    public static string ToDescription(this Enum value)
    {
        if (value != null)
        {
            Type type = value.GetType();
            string name = Enum.GetName(type, value);
            return GetDescription(type, name);
        }

        return "";
    }

    public static Dictionary<int, string> ToDescriptionDictionary<TEnum>()
    {
        Array values = Enum.GetValues(typeof(TEnum));
        Dictionary<int, string> dictionary = new Dictionary<int, string>();
        foreach (Enum item in values)
        {
            dictionary.Add(Convert.ToInt32(item), item.ToDescription());
        }

        return dictionary;
    }

    public static Dictionary<int, string> ToDictionary<TEnum>()
    {
        Array values = Enum.GetValues(typeof(TEnum));
        Dictionary<int, string> dictionary = new Dictionary<int, string>();
        foreach (Enum item in values)
        {
            dictionary.Add(Convert.ToInt32(item), item.ToString());
        }

        return dictionary;
    }

    public static TEnum Parse<TEnum>(string value) where TEnum : struct
    {
        Enum.TryParse<TEnum>(value, out var result);
        return result;
    }

    //
    // 摘要:
    //     返回枚举值的描述信息。
    //
    // 参数:
    //   value:
    //     要获取描述信息的枚举值。
    //
    // 返回结果:
    //     枚举值的描述信息。
    public static string GetEnumDesc<T>(int value)
    {
        Type typeFromHandle = typeof(T);
        DescriptionAttribute descriptionAttribute = null;
        string name = Enum.GetName(typeFromHandle, value);
        if (name != null)
        {
            FieldInfo field = typeFromHandle.GetField(name);
            if (field != null)
            {
                descriptionAttribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute), inherit: false) as DescriptionAttribute;
            }
        }

        if (descriptionAttribute != null && !string.IsNullOrEmpty(descriptionAttribute.Description))
        {
            return descriptionAttribute.Description;
        }

        return string.Empty;
    }
}



// FILE: Core/Converters.cs
using System.Globalization;
using System.Windows.Data;

namespace NetIngest.Core
{
    // Converter so sánh giá trị Binding (ViewMode) với Parameter truyền vào
    public class StringMatchToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string checkValue = value.ToString() ?? "";
            string targetValue = parameter.ToString() ?? "";

            // Nếu ViewMode == Parameter (ví dụ "Tree" == "Tree") thì trả về True
            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            // Nếu RadioButton được chọn (True), trả về Parameter để set lại ViewMode
            return (value is bool isChecked && isChecked) ? parameter : Binding.DoNothing;
        }
    }
}

using Xunit;
using System.Collections.ObjectModel;
using FinancialAccounting;
namespace FinAccTest
{
    public class PdfParserTests
    {
        [Fact]
        public void ParsePdfText_ParsesTransactionsCorrectly()
        {
            string pdfText = @"
������� �� ����� ��������� ������������� 3 �� 8���� �������� (���)���� ���������? � ��� ���������������������������� ������������� � ������ �ר������� � ������ ��������?������� ������� � ������ �ר��27.12.202413:46981314������ ��������407,0010 125,04
";     
            var result = PdfParser.ParsePdfText(pdfText);

            Assert.Single(result);

            Assert.Equal("27.12.2024", result[0].Date);
            Assert.Equal("������ ��������", result[0].Category);
            Assert.Equal("407,00", result[0].Amount);
            Assert.Equal("10 125,04", result[0].Balance);
        }
    }
}
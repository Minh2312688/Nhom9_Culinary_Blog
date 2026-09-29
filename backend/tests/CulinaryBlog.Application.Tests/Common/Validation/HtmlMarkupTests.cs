using CulinaryBlog.Application.Common.Validation;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Common.Validation;

/// <summary>
/// Unit test cho HtmlMarkup: chặn markup thật (<b>, <script>, <img src=x>, </b>, comment)
/// nhưng không chặn text thuần có ký tự so sánh như "Món < 30 phút".
/// </summary>
public class HtmlMarkupTests
{
    [Theory]
    [InlineData("<b>Bánh ngọt</b>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("<br/>")]
    [InlineData("<br />")]
    [InlineData("<b>Bánh Ngọt")]
    [InlineData("Bánh <br/> Ngọt")]
    [InlineData("</b>")]
    [InlineData("<a href=\"https://cdn.test/a.jpg\">x</a>")]
    [InlineData("<!-- chú thích -->")]
    [InlineData("<!DOCTYPE html>")]
    public void Contains_WithHtmlMarkup_ShouldReturnTrue(string value)
    {
        // Act
        var containsMarkup = HtmlMarkup.Contains(value);

        // Assert
        containsMarkup.Should().BeTrue(value);
    }

    [Theory]
    [InlineData("Đồ")]
    [InlineData("Bánh Ngọt")]
    [InlineData("Cà Phê Sữa Đá")]
    [InlineData("Món < 30 phút")]
    [InlineData("Bún <3")]
    [InlineData("Giá 4>2")]
    [InlineData("Bánh <b")]
    [InlineData("Đồ ăn (ngon) 100%")]
    [InlineData("Rau củ, trái cây!")]
    public void Contains_WithPlainText_ShouldReturnFalse(string value)
    {
        // Act
        var containsMarkup = HtmlMarkup.Contains(value);

        // Assert
        containsMarkup.Should().BeFalse(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Contains_WithMissingValue_ShouldReturnFalse(string? value)
    {
        // Act
        var containsMarkup = HtmlMarkup.Contains(value);

        // Assert
        containsMarkup.Should().BeFalse();
    }
}

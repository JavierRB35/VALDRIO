using System.Xml.Linq;

namespace Valdrio.Tests;

[TestClass]
public sealed class EmbeddedUnitStyleRegressionTests
{
    [TestMethod]
    public void ReparentedCompanyUnitFormDoesNotInheritModelInputBindings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ui-contracts", "CompanyFilePanel.xaml");
        var xaml = XDocument.Load(path);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace names = "http://schemas.microsoft.com/winfx/2006/xaml";

        var unitForm = xaml.Descendants(presentation + "StackPanel")
            .Single(element => (string?)element.Attribute(names + "Name") == "UnitCreationPanel");
        var resources = unitForm.Element(presentation + "StackPanel.Resources");
        Assert.IsNotNull(resources, "The movable unit form must isolate its implicit input styles.");

        foreach (var (targetType, baseStyle) in new[]
        {
            ("TextBox", "Input.Text"),
            ("ComboBox", "Input.Combo")
        })
        {
            var style = resources.Elements(presentation + "Style")
                .SingleOrDefault(element => (string?)element.Attribute("TargetType") == targetType);
            Assert.IsNotNull(style, "Missing scoped style for " + targetType);
            Assert.AreEqual("{StaticResource " + baseStyle + "}", (string?)style.Attribute("BasedOn"),
                "Unit input styles must use the shared theme, not model-specific styles.");
            Assert.IsFalse(style.Descendants(presentation + "Setter").Any(setter =>
                (string?)setter.Attribute("Property") is "IsEnabled" or "DisplayMemberPath"),
                "Unit controls must not inherit the model's InputsEnabled or Label bindings.");
        }

        foreach (var (controlName, controlType) in new[]
        {
            ("UnitCodeBox", "TextBox"),
            ("DimensionBox", "TextBox"),
            ("SymbolBox", "TextBox"),
            ("ScaleBox", "ComboBox")
        })
        {
            Assert.IsTrue(unitForm.Descendants(presentation + controlType)
                .Any(element => (string?)element.Attribute(names + "Name") == controlName),
                "Expected unit control is no longer inside the isolated style scope: " + controlName);
        }

        var scale = unitForm.Descendants(presentation + "ComboBox")
            .Single(element => (string?)element.Attribute(names + "Name") == "ScaleBox");
        Assert.IsNull(scale.Attribute("DisplayMemberPath"),
            "Scale options are integers and must not request a Label property.");
        Assert.AreEqual("{Binding IsIdle}",
            (string?)scale.Parent?.Attribute("IsEnabled"),
            "The company workspace, not the model style, must control unit input availability.");
    }
}

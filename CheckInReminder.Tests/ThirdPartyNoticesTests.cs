using CheckInReminder;

namespace CheckInReminder.Tests;

[TestClass]
public sealed class ThirdPartyNoticesTests
{
    [TestMethod]
    public void EmbeddedNotice_ExplainsMaterialRightsAndLegalLimits()
    {
        using var stream = typeof(AppSettings).Assembly.GetManifestResourceStream("CheckInReminder.THIRD_PARTY_NOTICES.md")!;
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        StringAssert.Contains(text, "使用声明与免责声明");
        StringAssert.Contains(text, "免费提供");
        StringAssert.Contains(text, "不等于取得第三方素材授权");
        StringAssert.Contains(text, "不得排除或限制的责任");
        StringAssert.Contains(text, "https://github.com/dong-dong-yao/da-ge-ka/issues");
    }

    [TestMethod]
    public void ApplicationAssembly_CarriesBongoCatAttributionAndFullMitPermission()
    {
        var assembly = typeof(AppSettings).Assembly;
        var resource = assembly.GetManifestResourceNames().SingleOrDefault(
            name => name.EndsWith(".THIRD_PARTY_NOTICES.md", StringComparison.Ordinal));
        Assert.IsNotNull(resource, "单文件发布的应用程序集必须自带第三方许可。");
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var notice = reader.ReadToEnd().ReplaceLineEndings("\n");

        StringAssert.Contains(notice, "https://github.com/ayangweb/BongoCat");
        StringAssert.Contains(notice, "Copyright (c) 2025 ayangweb");
        StringAssert.Contains(notice, """
            Permission is hereby granted, free of charge, to any person obtaining a copy
            of this software and associated documentation files (the "Software"), to deal
            in the Software without restriction, including without limitation the rights
            to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
            copies of the Software, and to permit persons to whom the Software is
            furnished to do so, subject to the following conditions:
            """.ReplaceLineEndings("\n"));
    }
}

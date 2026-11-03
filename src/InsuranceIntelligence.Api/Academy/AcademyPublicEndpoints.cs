using InsuranceIntelligence.Domain.Academy;

namespace InsuranceIntelligence.Api.Academy;

/// <summary>
/// What anyone may read without signing in: the course list, a course's page and its curriculum (titles and lengths, never
/// lesson text or any question). Prices come from the Academy operator's price list.
/// </summary>
public static class AcademyPublicEndpoints
{
    public static async Task<IResult> Courses(
        IAcademyContentProvider content, IAcademyPricingRepository pricing, AcademyOperator academyOperator, Payments.IPaymentGateway gateway, HttpContext http, CancellationToken ct)
    {
        var prices = await PricesAsync(pricing, academyOperator, ct);
        http.Response.Headers.CacheControl = "public, max-age=60";
        return Results.Ok(new { currency = AcademyPricing.Currency, paymentsConfigured = gateway.Configured, courses = content.Catalogue.Courses.Select(course => Summary(course, prices)) });
    }

    public static async Task<IResult> Course(
        string courseId, IAcademyContentProvider content, IAcademyPricingRepository pricing, AcademyOperator academyOperator, HttpContext http, CancellationToken ct)
    {
        var course = content.Course(courseId);
        if (course is null)
        {
            return Results.NotFound(new { message = "That course was not found." });
        }

        var prices = await PricesAsync(pricing, academyOperator, ct);
        http.Response.Headers.CacheControl = "public, max-age=60";
        return Results.Ok(Detail(course, prices));
    }

    internal static async Task<IReadOnlyDictionary<string, decimal>> PricesAsync(IAcademyPricingRepository pricing, AcademyOperator academyOperator, CancellationToken ct)
    {
        AcademyPricing? saved = null;
        try
        {
            saved = await pricing.GetAsync(academyOperator.Scope, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A price list that cannot be read must not hide the catalogue: courses fall back to the starting price.
        }

        return (saved ?? AcademyPricing.Seed(academyOperator.Scope)).WithDefaults().Prices
            .ToDictionary(price => price.CourseId, price => price.Amount, StringComparer.OrdinalIgnoreCase);
    }

    internal static decimal PriceOf(IReadOnlyDictionary<string, decimal> prices, string courseId) =>
        prices.TryGetValue(courseId, out var amount) ? amount : AcademyPricing.DefaultPrice(courseId);

    public static object Summary(AcademyCourse course, IReadOnlyDictionary<string, decimal> prices) => new
    {
        id = course.Id,
        number = course.Number,
        category = course.Category,
        title = course.Title,
        @short = course.Short,
        level = course.Level,
        duration = course.Duration,
        hue = course.Hue,
        icon = course.Icon,
        status = course.IsReady ? "ready" : "coming_soon",
        modules = course.Modules.Count,
        lessons = course.AllLessons.Count(),
        lessonIds = course.AllLessons.Select(lesson => lesson.Id),
        minutes = course.AllLessons.Sum(lesson => lesson.Minutes),
        price = PriceOf(prices, course.Id),
        currency = AcademyPricing.Currency,
        hasCertificate = course.IsReady,
        paidReleaseApproved = course.PaidReleaseApproved,
    };

    public static object Detail(AcademyCourse course, IReadOnlyDictionary<string, decimal> prices) => new
    {
        id = course.Id,
        number = course.Number,
        category = course.Category,
        title = course.Title,
        @short = course.Short,
        description = course.Description,
        audience = course.Audience,
        level = course.Level,
        duration = course.Duration,
        hue = course.Hue,
        icon = course.Icon,
        status = course.IsReady ? "ready" : "coming_soon",
        outcomes = course.Outcomes,
        who = course.Who,
        iipAreas = course.IipAreas,
        requirements = course.Requirements,
        contentStatus = course.ContentStatus,
        paidReleaseApproved = course.PaidReleaseApproved,
        price = PriceOf(prices, course.Id),
        currency = AcademyPricing.Currency,
        modules = course.Modules.Select(module => new
        {
            id = module.Id,
            title = module.Title,
            lessons = module.Lessons.Select(lesson => new { id = lesson.Id, number = lesson.Number, title = lesson.Title, minutes = lesson.Minutes, hasQuiz = lesson.Quiz is not null }),
            assessment = module.Assessment is null ? null : new { id = module.Assessment.Id, title = module.Assessment.Title, questions = module.Assessment.Questions.Count, passMark = module.Assessment.PassMark, maxAttempts = module.Assessment.MaxAttempts },
        }),
        check = course.Check is null ? null : new { id = course.Check.Id, title = course.Check.Title, questions = course.Check.Questions.Count, passMark = course.Check.PassMark },
        finalTask = course.FinalTask is null ? null : new { id = course.FinalTask.Id, title = course.FinalTask.Title, passMark = course.FinalTask.PassMark, outOf = course.FinalTask.OutOf },
        outline = course.Outline,
        sources = course.Sources,
    };
}

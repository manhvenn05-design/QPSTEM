using Microsoft.AspNetCore.Mvc;
using STEM.Web.Models;

namespace STEM.Web.Controllers;

public class NewsController : Controller
{
    public IActionResult Index()
    {
        var articles = GetArticles();
        var featuredArticles = GetFeaturedArticles();

        return View(new NewsIndexViewModel
        {
            Articles = articles,
            FeaturedArticles = featuredArticles,
            Categories =
            [
                new NewsCategoryItemViewModel { Name = "STEM", Count = 24 },
                new NewsCategoryItemViewModel { Name = "Giáo dục", Count = 18 },
                new NewsCategoryItemViewModel { Name = "Công nghệ", Count = 32 },
                new NewsCategoryItemViewModel { Name = "Tin tức QPSTEM", Count = 12 }
            ],
            Tags = ["#LậpTrình", "#Robotics", "#AI", "#KhoaHọc", "#KỹNăngSống", "#Creative"]
        });
    }

    public IActionResult Details(string slug)
    {
        var articles = GetArticles();
        var selectedArticle = articles.FirstOrDefault(article => article.Slug == slug) ?? articles[0];

        var viewModel = new NewsDetailViewModel
        {
            Article = selectedArticle,
            HeroImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuAh4KeeLt1bW0Q2fXJsk86KiXCQIQ_UggODhvp06CoeYB_X1bEcOs0l3Lw2DHiJtl_rCuFcyPOEOkE4OIBG3Xs35wpzuHeMd3gsdk2s932Jhr2jymbJYMlDIhSBisBo1rEn_Zn9FJOTeovHC-ulhRJKT-EalWKgkFyYDhRhRjONE5-14otxz0pOqtXaBlb9Czqlc4_PKGOWnDf_3Mxmb40A0Vc6KZeoli8Jqz8I3ysCXfECiU-7sAgeSTps5niD7yCRlR8r1Bu6hcwz",
            Sections =
            [
                new NewsArticleSectionViewModel
                {
                    Heading = "STEM là gì?",
                    Paragraphs =
                    [
                        "STEM là viết tắt của các từ Science (Khoa học), Technology (Công nghệ), Engineering (Kỹ thuật) và Mathematics (Toán học). Thay vì dạy bốn môn học như các đối tượng tách biệt và rời rạc, STEM kết hợp chúng vào một mô hình học tập gắn kết dựa trên các ứng dụng thực tế."
                    ]
                },
                new NewsArticleSectionViewModel
                {
                    Heading = "Phát triển tư duy logic",
                    Paragraphs =
                    [
                        "Giáo dục STEM không chỉ cung cấp kiến thức hàn lâm mà còn tập trung vào việc rèn luyện kỹ năng thực hành. Dưới đây là những lợi ích then chốt mà trẻ nhận được:"
                    ]
                }
            ],
            KeyPoints =
            [
                "Tư duy phản biện: Trẻ học cách đặt câu hỏi và tìm kiếm giải pháp dựa trên dữ liệu thực tế.",
                "Khả năng sáng tạo: Thông qua việc lắp ráp robot và lập trình, trẻ được tự do hiện thực hóa ý tưởng.",
                "Kỹ năng giải quyết vấn đề: STEM giúp trẻ không sợ hãi trước những thử thách kỹ thuật phức tạp."
            ],
            AuthorBio = "Chuyên gia tư vấn giáo dục STEM tại QPSTEM với hơn 10 năm kinh nghiệm trong lĩnh vực đào tạo trẻ em.",
            FeaturedArticles = GetDetailFeaturedArticles(),
            Tags = ["#LậpTrình", "#Robotics", "#KỹNăngSống", "#ToánHọc", "#KhoaHọc"],
            RelatedArticles = articles.Where(article => article.Slug != selectedArticle.Slug).Take(3).ToList()
        };

        return View(viewModel);
    }

    private static IReadOnlyList<NewsArticleCardViewModel> GetArticles()
    {
        return
        [
            new NewsArticleCardViewModel
            {
                Slug = "tuong-lai-robotics-2024",
                Title = "Tương lai của Robotics trong giáo dục tiểu học năm 2024",
                Excerpt = "Robotics không còn là điều xa lạ, nó đang trở thành nòng cốt trong việc phát triển tư duy logic và giải quyết vấn đề cho trẻ ngay từ lớp 1.",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuChAXntQAvEfIkH6odWbRKOD6_fxBgEoUCTFU9J94rw2CE42ETyZHoUBU5u57k0DcUefH1W8pX8QqbNh9S0BxnZdTJnxOn-dZbsWsDjZ8hXjvqbZZ_TkMf9uinK4PYw0DmLMOAbYsfLSfxVXmrIUicIBiydjFjrMozCGSYhLPdVVTMN4pnRb829v3oOwjml1kLMKYBbfo20jzj8FrHL4l60aoTXwMZ0BVoAKiARIK-URZWQnwdvRJ4a5bOLNNoMHea5VzkNUhPEia7E",
                Category = "Công nghệ",
                AuthorName = "Minh Anh",
                AuthorAvatarUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuDLC4Js7pBIXDwOkZ7ezgDEARCSxyxZFtIUtPeKxSok1vSDRyrATlNC49p_gCe-1Jt37EQRs5YXIcI5y1khh4UX2ZN2jz-JbvivqFbc2xljWjbE_GMfjO3hczrXt3Zy-lMjnpTu-rbENdpHpe-H5chrzPiAyAcW85Qy5wklfFuBK0P9l0GPI-6A1lU0SWucjhg-AXDYJosEP73-RbsReKlXVYHuy1Y41yQE2eBsqHBRGf0VOteploT-T4EpGleBSppCP9b1HkPhY0-9",
                PublishedDateText = "12 Th05, 2024",
                ReadTimeText = "5 phút đọc"
            },
            new NewsArticleCardViewModel
            {
                Slug = "5-phuong-phap-day-stem-tai-nha",
                Title = "5 Phương pháp dạy STEM hiệu quả tại nhà cho phụ huynh",
                Excerpt = "Làm thế nào để khơi gợi niềm đam mê khoa học cho trẻ mà không cần đến những phòng thí nghiệm đắt tiền? Hãy cùng khám phá ngay.",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuDK56-RarmF_KHBL6IYGK0Q_Ng36vXiXXIcWrtcjHeOTXpJuT0hOcJJ2JYC3_kQa6tttvyVr7MjkWuq1I3jYGj3OIT0V-iRvgkcVyjg6tae_mUOEEpBse_4u_deQSQKee1Jnzg2adHeuyOOXusAOwMl2tJB40e7l1X06Kl_9iTKQNlss_QrHi6G2_7lFbGEDSXKIdoS7ARTuVAW9JOsZ2rTb-uhNWdpMgAnRjMOnEJZVOUjeQztmIeffbSGDDnKqWRTl4dVbB67q_2W",
                Category = "Giáo dục",
                AuthorName = "Thùy Chi",
                AuthorAvatarUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuCE5hcch2nMLcskCu4V5T3F-o6pGN2tWJu2qgFaf13G4r7c33Y3t47v7HzEui5_VplEBNAI5cdqRnXwju3hi507EeCnVW9QR67kLfwi9Dy8yW6mCGedORmUgYaeDvSEMlmHaYr8U3y5V9hCNtdgoX1W7GUx9XLZW1n6ysYE3S5nfFdUuvvS7_HayELdkFY2JmEV3xbIe24YE0S8jhEQAD3wpLC6aDLn83rLTnTV5hUfQzo30uyLJY0clEfqNXVOTSG4HqvODJrBH_-z",
                PublishedDateText = "10 Th05, 2024",
                ReadTimeText = "8 phút đọc"
            },
            new NewsArticleCardViewModel
            {
                Slug = "tai-sao-python-tot-nhat-cho-stem",
                Title = "Tại sao Python là ngôn ngữ tốt nhất để bắt đầu STEM?",
                Excerpt = "Cú pháp đơn giản, cộng đồng lớn mạnh và tính ứng dụng cao khiến Python trở thành lựa chọn số 1 cho giáo dục lập trình.",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuA3K2DXzi9VwPsasIjhDu9fFngXhXrHkcn00aFPwIQKLHIo-KIaRPU_9ick2A5T6W0RQTxhgL5ptqqhST24_e1rp8Q4HWv83qdRB9QqGeJjp2ffyaNWxzvCe_nX_womw5A137uR94OJwOgXZtZlyhRHXfAGGeeJyV4hKJFUb9CQxRiIEwJhbSWDOnJfH0zz6IEBBmf1hGmhAySqlH1sG_vft3wusq4xOkoSMlDyqB-0Wj82ad3n9KZOWcvpFOQekJRWvg8yeA_7i0Jf",
                Category = "Lập trình",
                AuthorName = "Hoàng Nam",
                AuthorAvatarUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuAOsSWfy7FKgeueDdqtgcCWv9atF_WRWrXaRWhbefGBdfisJKXkJOkzkkM4M-L9zpcYZRNPGwIYEZ11alMmqtd8qpgJpbigvFQ7-blUNcF0M3kpobx-7tBs4kuI0VgjgTiBrBgbt-PGghkU2B7FljkYW9o2TzIgZiexc89PZMjuLDvcsDYTu_pkj4vpDmniss7cKlZe9dDPz-q4r2xkD5J-2WUE1jUaaaCYl9fcgp7VRb7D2z4S6L7Oz0dc4K-siULcWGdjaT0UYGcq",
                PublishedDateText = "08 Th05, 2024",
                ReadTimeText = "12 phút đọc"
            },
            new NewsArticleCardViewModel
            {
                Slug = "thi-nghiem-sinh-hoc-vat-lieu-tai-che",
                Title = "Thí nghiệm sinh học thú vị với vật liệu tái chế tại QPSTEM",
                Excerpt = "Học sinh được trải nghiệm thực tế việc tái chế nhựa để tạo nên hệ sinh thái thu nhỏ độc đáo ngay trong lớp học.",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuDEqALkS8SNrzdZ4vBbrrDYesFFPS9xDMVEwocadXe8HWumT2nlv1B3apis46jp4reygnmzRnGiF5dRVqqnoXBJ_bf-LNI4ikeDekMFpThTXcq5tMkBCxGItb0p9PKe2rHEA_jBqmjry3-bV122uBifzF8-CedFWPu-pQK0rCunnUvoFPrwwe4reRPxQtBuwqbNhbPqxmgvEEGPISZ4qAc_yxxNJL2xsHJ8B5ww2KelBAiLd-sgStx-xNrE_3UHn0Y2rNxJ6k2w6caM",
                Category = "Khoa học",
                AuthorName = "Lan Hương",
                AuthorAvatarUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuB_a3fVfz8KvHBoA-Hu13x-lmnyHkPXr8lcYyWz0VU6KTHMz6xGOrNZK1JzIl0xZuvPFcmjwSAMaQjH7BbgHzWdf1F0W_smVDSW090_m0Py7QcoRAMguelUaZvUC93HoqxBzSFEAdUYj_89UcMNBeO7_5qiHXCsmAxpgOMugYW7lBUIwY_tqzJi1kouHwc7NGxAlODg3SM0MiNajlSmz2pt1dJX7Kh6jdxv6LBQB3Y3S2rc6l8Xe141mGGyQYkLtslxPDtbj36lXV2O",
                PublishedDateText = "05 Th05, 2024",
                ReadTimeText = "6 phút đọc"
            }
        ];
    }

    private static IReadOnlyList<NewsSidebarItemViewModel> GetFeaturedArticles()
    {
        return
        [
            new NewsSidebarItemViewModel
            {
                Title = "Lộ trình học AI cho học sinh trung học",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuDhsaxbtbMlNCNqnF73IfrLaoJBTJAO-V348wJkJzJ2IxahahAK7F51Rji4J0WqqLgLLojZqSBa-WisXm5EyGpNNCjGvLLECSqh0EsHwA_357aIV7bG-uozEEix7ctDS6h_eqHXFptTPsUXAoZuZCunFlUmT8SKC8jFbQ5R5Hb4j9qnKZoWe3vNerseXHTGTOj_ZXjuURzJs4_Q0lwvVlfb5LDvG3EajipMEF932Xco7DijAiESrhipkWuaJYPhI5UKNcoINa3Igsej",
                PublishedDateText = "20 Th04, 2024"
            },
            new NewsSidebarItemViewModel
            {
                Title = "Thiết kế mạch điện thông minh cơ bản",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuAwyRAKj6v32mIEekOFX3oY4XFrJWgbqE4f8axDvn29OOSGoZtjZDNFrQUywan-cBIbT06pSg9T73m_t6o4r4kr3u2Kkrnq0teUG6zm6ftT67b3YQheHw5EXvw4G04PCxVnRiof28r5ms7qX4zb2GfVmgvYOO1oSo8vLGNZFHaIQwxHWeYaj9MbyuscjkQsBsEBO79Anb9x8BjK7XvskD07SdgttBG8r4b3tZFS35gFbuL5uxNHK65aphpm4CqCaw3IE1KRDJSjFMr-",
                PublishedDateText = "18 Th04, 2024"
            },
            new NewsSidebarItemViewModel
            {
                Title = "Ứng dụng in 3D trong đời sống hằng ngày",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuBhkpocVmIObRw7E2A6nANo-IBqmIEXH8TdvqC0laOs7qfDxVuR7egL3mR3cRjTGRTi0ti98h-6IouPEtGnnio25vE4TDuhVZcivM8brCb3FAof5NIY7EVaio9yZLAzcASCG5Vq8KjPepQ52h1LZsTaUdn3Bbx5eSA1sq_pZTpWl3Q5EiQh3S8ICTD3LGbKowUZgpigZxsGtsMnYduEW03hiNgewDVcO6_D-uqoqHJmnAQNkeXcrZertjvgaYm4Pf2Ez9NrYZi3AFZ5",
                PublishedDateText = "15 Th04, 2024"
            }
        ];
    }

    private static IReadOnlyList<NewsSidebarItemViewModel> GetDetailFeaturedArticles()
    {
        return
        [
            new NewsSidebarItemViewModel
            {
                Title = "Xu hướng giáo dục 4.0 trong năm 2024",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuAmbGnvRII5mTGlyf083e1BefwHrB9S3_SH-SSzen9_Ci6Nq53bqYmKfjztVeu0Ivh48TnXwkSNMHy12I-MXWJHzhtSVhpzsO6QTxxRgPWg-5FhggmU0NxyPW5pL7OA3HMHheIEb_-tmGZBEhlV5HHsFCqgybBDFaDKIb5v_h7DjrFf1CAw1N7ZLSt0LXWw4kFb1ehy8ydTIsP5BfxG4Xy5Xb33QfISLxoVMQ08LG1M_kLsgd2y8m-1L_qwqhfqNlFyF80YbvKk8eJq",
                PublishedDateText = "20/05/2024"
            },
            new NewsSidebarItemViewModel
            {
                Title = "Lợi ích của việc học lập trình sớm",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuBJhNAUDvQKlWVhn9yiy1sDaVZ82U3U97TNGnQc9RN8zrV3LiujizvLSFXJeqC3iEZk3eavA3DpnMG1C2PA6PfZ29qdyzPVe_G4moOry3ScpXD1KvHyW7xwYyqRsfilUT330JXZAHojEaUbBj6TMOy-Ny2nrxz0ogM09ctME96zzDoPmWWMtWWgA9jM6rDxtW7X6v3IRNvaU7E4wNgzh06rZoDsxrB44LdbKVmLznY4pl5nbonQZwmtrt8N8uP_PpPe4AK_xV9qZYer",
                PublishedDateText = "18/05/2024"
            },
            new NewsSidebarItemViewModel
            {
                Title = "Kỹ năng mềm cần thiết cho tương lai",
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuBFba1UKfCpukwdej5E8FytHqb5DVhPR17425UpytQypXFQzHb4kVDTlk0iI5QmkCPaz8mNBRHqCnmmSXaSgnHy--mleSu39YVKlN1vMi87F_b3dYCTXJujQpO4SXS1euO6oncyxoVhcXivjK7o8ygJ27W0NUGQ6CSkTwwxW48AaP5MUbEdbKNUzBAdLMzAw3xQCkC-TrCPC8e5z_EFRZ2k-w5FnkHmws8aJwYhSvNUao2GAHsCFX6fBpf4l_v4LlgG7w0aAQbsBOGo",
                PublishedDateText = "17/05/2024"
            }
        ];
    }
}

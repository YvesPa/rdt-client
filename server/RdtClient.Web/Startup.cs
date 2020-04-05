using System.Threading.Tasks;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RdtClient.Data;
using RdtClient.Data.Data;
using RdtClient.Data.Models.Internal;
using RdtClient.Service.Services;

namespace RdtClient.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var appSettings = new AppSettings();
            Configuration.Bind(appSettings);

            services.AddSingleton(appSettings);

            services.AddDbContext<DataContext>(options => options.UseSqlite(DataContext.ConnectionString));

            services.AddControllers();

            services.AddSpaStaticFiles(configuration => { configuration.RootPath = "wwwroot"; });

            services.AddHttpContextAccessor();

            services.AddCors(options =>
            {
                options.AddPolicy("Dev",
                                  builder =>
                                  {
                                      builder.AllowAnyHeader()
                                             .AllowAnyMethod()
                                             .AllowAnyOrigin();
                                  });
            });

            services.AddHttpsRedirection(options => { options.HttpsPort = 443; });

            services.AddHttpClient();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(options => { options.SlidingExpiration = true; });

            services.AddAuthorization();

            services.AddIdentity<IdentityUser, IdentityRole>(options =>
                    {
                        options.User.RequireUniqueEmail = false;
                        options.Password.RequiredLength = 10;
                        options.Password.RequireUppercase = false;
                        options.Password.RequireLowercase = false;
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequiredUniqueChars = 5;
                    })
                    .AddEntityFrameworkStores<DataContext>()
                    .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(options =>
            {
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
            });

            services.AddHangfire(configuration => configuration
                                                  .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                                                  .UseSimpleAssemblyNameTypeSerializer()
                                                  .UseRecommendedSerializerSettings()
                                                  .UseMemoryStorage());

            services.AddHangfireServer();

            DiConfig.Config(services);
            Service.DiConfig.Config(services);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, DataContext dataContext, IScheduler scheduler)
        {
            if (env.IsDevelopment())
            {
                app.UseCors("Dev");
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHttpsRedirection();
            }

            app.UseDefaultFiles();

            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseHangfireServer();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

            app.UseSpa(spa => { spa.Options.SourcePath = "wwwroot"; });

            dataContext.Migrate();

            scheduler.Start();
        }
    }
}
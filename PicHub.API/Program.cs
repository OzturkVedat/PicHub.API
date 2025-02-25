using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PicHub.API.Services;

var builder = WebApplication.CreateBuilder(args);

var ssmClient = new AmazonSimpleSystemsManagementClient(Amazon.RegionEndpoint.EUNorth1);

var request = new GetParametersRequest
{
    Names = new List<string>
    {
        "/pichub/aws_access_key",
        "/pichub/aws_secret_key",
        "/pichub/aws_region",
        "/pichub/user_pool_client_id",
        "/pichub/user_pool_client_secret",
        "/pichub/jwt_authority",
    },
    WithDecryption = true
};

var response = await ssmClient.GetParametersAsync(request);
var configValues = response.Parameters.ToDictionary(p => p.Name, p => p.Value);
builder.Configuration.AddInMemoryCollection(configValues);


builder.Services.AddScoped<IAmazonCognitoIdentityProvider>(sp =>
{
    var credentials = new BasicAWSCredentials(
        builder.Configuration["/pichub/aws_access_key"],
        builder.Configuration["/pichub/aws_secret_key"]
    );
    return new AmazonCognitoIdentityProviderClient(credentials, Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["/pichub/aws_region"]));
});

builder.Services.AddScoped<IAmazonS3>(sp =>
{
    var credentials = new BasicAWSCredentials(
        builder.Configuration["/pichub/aws_access_key"],
        builder.Configuration["/pichub/aws_secret_key"]);
    return new AmazonS3Client(credentials, Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["/pichub/aws_region"]));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.Authority = builder.Configuration["/pichub/jwt_authority"];
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidAudience = builder.Configuration["/pichub/user_pool_client_id"],
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = "cognito:groups"    // check the role claim
        };
        // o.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<CognitoService>();
builder.Services.AddScoped<S3Service>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddMemoryCache();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid access token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference= new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


using FastEndpoints;
using DbView.Core;
using DbView.Application;

namespace Users.Creates
{
    internal sealed class UserCreateEndpoint : Endpoint<CreateUserRequest, CreateUserResponse, Mapper>
    {
        //MapsterMapper.IMapper mapper;
        
        IUserAppService userAppService;
        IConfiguration configuration;
        public UserCreateEndpoint(IUserAppService userAppService, IConfiguration configuration)
        {
            this.userAppService = userAppService;
            this.configuration = configuration;
        }
        public override void Configure()
        {
            Put("/user/create");
            //AllowAnonymous();
            //Policies("admin");Roles("admin");
            //Description(b => b
            //   .Produces<CreateUserResponse>(201)
            //    .ProducesProblem(400)
            //    .WithTags("Users"));

            Validator<CreateUserValidator>();
            // 摘要信息
            // Swagger中摘要信息
            //Summary(s =>
            //{
            //    s.Summary = "接口名称";
            //    s.Description = "接口描述";
            //    s.ResponseExamples[201] = new CreateUserResponse
            //    {
            //    };
            //});
        }

        public override async Task HandleAsync(CreateUserRequest r, CancellationToken c)
        {
                //var userDto = await userAppService.CreateUserAsync(new DbView.Application.Users.DTOs.UserCreateDto(), c);
                Response = new CreateUserResponse
                {
                     Message = "Created",
                };
        }
    }
}


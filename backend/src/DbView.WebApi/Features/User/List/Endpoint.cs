
using FastEndpoints;
using DbView.Core;
using DbView.Application;

namespace Users.Lists
{
    internal sealed class UserListEndpoint : Endpoint<ListUserRequest, ListUserResponse, Mapper>
    {
        //MapsterMapper.IMapper mapper;
        
        IUserAppService userAppService;
        IConfiguration configuration;
        public UserListEndpoint(IUserAppService userAppService, IConfiguration configuration)
        {
            this.userAppService = userAppService;
            this.configuration = configuration;
        }
        public override void Configure()
        {
            Get("/user/list");
            //AllowAnonymous();
            //Policies("admin");Roles("admin");
            //Description(b => b
            //   .Produces<ListUserResponse>(201)
            //    .ProducesProblem(400)
            //    .WithTags("Users"));

            Validator<ListUserValidator>();
            // 摘要信息
            // Swagger中摘要信息
            //Summary(s =>
            //{
            //    s.Summary = "接口名称";
            //    s.Description = "接口描述";
            //    s.ResponseExamples[201] = new ListUserResponse
            //    {
            //    };
            //});
        }

        public override async Task HandleAsync(ListUserRequest r, CancellationToken c)
        {
                Response = new ListUserResponse
                {
                     Message = "Not implemented yet",
                };
        }
    }
}


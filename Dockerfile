# Builds the LearnEngine MCP server as a Native AOT binary and runs it on a small image with no SDK and no shell.
# Run it with a volume for /data, where the histories, scores and compliance logs are kept between runs:
#   docker run -i --rm -v learnengine:/data learnengine-mcp

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble-aot AS build
ARG TARGETARCH
WORKDIR /src

# Restored on their own first, so a change to the code does not download the packages again.
COPY Directory.Packages.props LearnEngine.slnx ./
COPY src/LearnEngine.Core/LearnEngine.Core.csproj src/LearnEngine.Core/
COPY src/LearnEngine.Mcp/LearnEngine.Mcp.csproj src/LearnEngine.Mcp/
RUN rid=linux-x64 && if [ "$TARGETARCH" = "arm64" ]; then rid=linux-arm64; fi \
    && dotnet restore src/LearnEngine.Mcp/LearnEngine.Mcp.csproj -r $rid

COPY src/ src/
RUN rid=linux-x64 && if [ "$TARGETARCH" = "arm64" ]; then rid=linux-arm64; fi \
    && dotnet publish src/LearnEngine.Mcp/LearnEngine.Mcp.csproj -c Release -r $rid --no-restore -o /app \
    && mkdir /data

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/LearnEngine.Mcp ./

# Owned by the image's non root app user, so a new named volume mounted here starts out writable by it.
COPY --from=build --chown=1654:1654 /data /data

ENV LEARNENGINE_DATA=/data \
    LEARNENGINE_LEARNER=me
VOLUME /data
USER 1654
ENTRYPOINT ["/app/LearnEngine.Mcp"]

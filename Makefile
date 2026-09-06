.PHONY: build clean format format-check help lint pack restore test

.DEFAULT_GOAL := help

SOLUTION := AgentKit.slnx
CONFIGURATION ?= Release

help:
	@echo "AgentKit - Available Make Targets"
	@echo "================================="
	@echo "  make restore       Restore .NET and Node.js dependencies"
	@echo "  make build         Build all projects"
	@echo "  make test          Run all test projects when present"
	@echo "  make pack          Create local NuGet packages"
	@echo "  make lint          Check C# and repository documentation"
	@echo "  make format        Format C# and repository documentation"
	@echo "  make format-check  Check formatting without changing files"
	@echo "  make clean         Clean .NET output"

restore:
	@echo "📦 Restoring dependencies..."
	@dotnet restore $(SOLUTION)
	@npm ci
	@echo "✅ Dependencies restored."

build: restore
	@echo "🔨 Building AgentKit..."
	@dotnet build $(SOLUTION) --configuration $(CONFIGURATION) --no-restore
	@echo "✅ Build complete."

test: build
	@echo "🧪 Running tests..."
	@if find tests -name '*.csproj' -type f -print -quit 2>/dev/null | grep -q .; then \
		dotnet test --solution $(SOLUTION) --configuration $(CONFIGURATION) --no-build --timeout 300s; \
	else \
		echo "ℹ️  No test projects exist yet."; \
	fi
	@echo "✅ Test phase complete."

pack: build
	@echo "📦 Packing AgentKit..."
	@dotnet pack $(SOLUTION) --configuration $(CONFIGURATION) --no-build --no-restore -p:PackageOutputPath=$(CURDIR)/artifacts/packages
	@echo "✅ Packages written to artifacts/packages."

lint: restore
	@echo "🔍 Checking source and documentation..."
	@dotnet format $(SOLUTION) --verify-no-changes --no-restore --verbosity diagnostic
	@npm run format:check
	@npm run lint:markdown
	@echo "✅ All lint checks passed."

format: restore
	@echo "✨ Formatting source and documentation..."
	@dotnet format $(SOLUTION) --no-restore
	@npm run format
	@echo "✅ Formatting complete."

format-check: restore
	@dotnet format $(SOLUTION) --verify-no-changes --no-restore
	@npm run format:check

clean:
	@dotnet clean $(SOLUTION) --verbosity minimal

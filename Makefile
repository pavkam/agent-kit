.PHONY: build clean coverage coverage-check format format-check help lint pack restore test

.DEFAULT_GOAL := help

SOLUTION := AgentKit.slnx
CONFIGURATION ?= Release
COVERAGE_DIR := artifacts/coverage
COVERAGE_MINIMUM_LINE ?= 90

help:
	@echo "AgentKit - Available Make Targets"
	@echo "================================="
	@echo "  make restore       Restore .NET and Node.js dependencies and tools"
	@echo "  make build         Build all projects"
	@echo "  make test          Run all test projects when present"
	@echo "  make coverage      Run all tests with code coverage and generate an HTML/text report"
	@echo "  make coverage-check  Run coverage and fail if line coverage is below COVERAGE_MINIMUM_LINE"
	@echo "  make pack          Create local NuGet packages"
	@echo "  make lint          Check C# and repository documentation"
	@echo "  make format        Format C# and repository documentation"
	@echo "  make format-check  Check formatting without changing files"
	@echo "  make clean         Clean .NET output"

restore:
	@echo "📦 Restoring dependencies..."
	@dotnet restore $(SOLUTION)
	@dotnet tool restore
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

coverage: build
	@echo "🧪 Running tests with code coverage..."
	@find tests -type d -name TestResults -prune -exec rm -rf {} + 2>/dev/null; \
	rm -rf $(COVERAGE_DIR); \
	dotnet test --solution $(SOLUTION) --configuration $(CONFIGURATION) --no-build --timeout 300s \
		-- --coverage --coverage-output-format cobertura
	@echo "📊 Merging coverage reports..."
	@dotnet tool run reportgenerator \
		-reports:"tests/**/TestResults/*.cobertura.xml" \
		-targetdir:"$(COVERAGE_DIR)/report" \
		-reporttypes:"Html;TextSummary;Cobertura" \
		-assemblyfilters:"+AgentKit*"
	@echo ""
	@cat $(COVERAGE_DIR)/report/Summary.txt
	@echo ""
	@echo "✅ Coverage report written to $(COVERAGE_DIR)/report/index.html"

coverage-check: coverage
	@echo "🚦 Checking line coverage against minimum of $(COVERAGE_MINIMUM_LINE)%..."
	@line_rate=$$(grep -o 'line-rate="[0-9.]*"' $(COVERAGE_DIR)/report/Cobertura.xml | head -1 | grep -o '[0-9.]*'); \
	percent=$$(awk -v r="$$line_rate" 'BEGIN { printf "%.2f", r * 100 }'); \
	echo "Line coverage: $$percent% (minimum: $(COVERAGE_MINIMUM_LINE)%)"; \
	awk -v p="$$percent" -v m="$(COVERAGE_MINIMUM_LINE)" 'BEGIN { exit !(p+0 >= m+0) }' || \
		(echo "❌ Coverage $$percent% is below the required $(COVERAGE_MINIMUM_LINE)%." && exit 1)
	@echo "✅ Coverage meets the required minimum."

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
	@find tests -type d -name TestResults -prune -exec rm -rf {} + 2>/dev/null || true
	@rm -rf $(COVERAGE_DIR)

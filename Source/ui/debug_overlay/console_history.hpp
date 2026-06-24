#pragma once

#include <string>
#include <string_view>
#include <vector>

namespace devilution {

class DebugConsoleHistory {
public:
	void Push(std::string_view command);
	std::string_view Previous(std::string_view draft);
	std::string_view Next();

private:
	std::vector<std::string> commands_;
	std::string draft_;
	int index_ = -1;
};

} // namespace devilution

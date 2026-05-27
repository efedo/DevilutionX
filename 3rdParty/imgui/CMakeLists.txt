include(functions/FetchContent_ExcludeFromAll_backport)
include(FetchContent)

FetchContent_Declare_ExcludeFromAll(imgui
  URL https://github.com/ocornut/imgui/archive/refs/tags/v1.92.8.tar.gz
  URL_HASH SHA256=fecb33d33930e12ff53a34064e9d3a06c8f7c3e04408f14cd36c80e3faac863b
  SOURCE_SUBDIR devilutionx-no-cmake
)

FetchContent_MakeAvailable_ExcludeFromAll(imgui)

set(imgui_sources
  "${imgui_SOURCE_DIR}/imgui.cpp"
  "${imgui_SOURCE_DIR}/imgui_draw.cpp"
  "${imgui_SOURCE_DIR}/imgui_tables.cpp"
  "${imgui_SOURCE_DIR}/imgui_widgets.cpp"
)

if(USE_SDL3)
  list(APPEND imgui_sources
    "${imgui_SOURCE_DIR}/backends/imgui_impl_sdl3.cpp"
    "${imgui_SOURCE_DIR}/backends/imgui_impl_sdlrenderer3.cpp"
  )
else()
  list(APPEND imgui_sources
    "${imgui_SOURCE_DIR}/backends/imgui_impl_sdl2.cpp"
    "${imgui_SOURCE_DIR}/backends/imgui_impl_sdlrenderer2.cpp"
  )
endif()

add_library(imgui STATIC EXCLUDE_FROM_ALL ${imgui_sources})
add_library(imgui::imgui ALIAS imgui)

target_include_directories(imgui PUBLIC
  "${imgui_SOURCE_DIR}"
  "${imgui_SOURCE_DIR}/backends"
)

target_link_libraries(imgui PUBLIC DevilutionX::SDL)

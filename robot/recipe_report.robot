*** Settings ***
Documentation     Report verification for RandomDataSeeder output.
Library           RequestsLibrary
Library           Collections
Suite Setup       Create Session    api    ${BASE_URL}

*** Variables ***
${BASE_URL}        http://localhost:5000
${PAGE_SIZE}       50

*** Test Cases ***
Verify Minimum Category Dataset
    [Documentation]    Requires at least 20 categories created by the async report seeder.
    ${response}=    GET On Session    api    url=/api/v1/categories/?pageSize=${PAGE_SIZE}    expected_status=200
    ${body}=    Set Variable    ${response.json()}
    ${count}=    Get Length    ${body}[items]
    Should Be True    ${count} >= 20    Expected at least 20 categories, got ${count}

Verify Minimum Recipe Dataset
    [Documentation]    Requires at least 100 recipes created by the async report seeder.
    ${response}=    GET On Session    api    url=/api/v1/recipes/?pageSize=${PAGE_SIZE}    expected_status=200
    ${body}=    Set Variable    ${response.json()}
    ${count}=    Set Variable    ${body}[totalCount]
    Should Be True    ${count} >= 100    Expected at least 100 recipes, got ${count}

Verify Recipe Children Dataset
    [Documentation]    Every sampled recipe must expose at least 10 ingredients and 5 steps.
    ${response}=    GET On Session    api    url=/api/v1/recipes/?pageSize=${PAGE_SIZE}&page=1    expected_status=200
    ${recipes}=    Set Variable    ${response.json()}[items]
    ${response_page_2}=    GET On Session    api    url=/api/v1/recipes/?pageSize=${PAGE_SIZE}&page=2    expected_status=200
    ${recipes_page_2}=    Set Variable    ${response_page_2.json()}[items]
    Append To List    ${recipes}    @{recipes_page_2}
    FOR    ${recipe}    IN    @{recipes}
        ${detail_response}=    GET On Session    api    url=/api/v1/recipes/${recipe}[slug]    expected_status=200
        ${detail}=    Set Variable    ${detail_response.json()}
        ${ingredient_count}=    Get Length    ${detail}[ingredients]
        ${step_count}=    Get Length    ${detail}[steps]
        Should Be True    ${ingredient_count} >= 10    ${recipe}[slug] has fewer than 10 ingredients
        Should Be True    ${step_count} >= 5    ${recipe}[slug] has fewer than 5 steps
    END
